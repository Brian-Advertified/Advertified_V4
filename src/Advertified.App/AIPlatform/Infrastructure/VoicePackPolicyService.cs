using System.Text.Json;
using System.Text.RegularExpressions;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class VoicePackPolicyService : IVoicePackPolicyService
{
    private static readonly string[] SaLanguages = { "english", "zulu", "afrikaans", "xhosa" };
    private static readonly Regex[] UsSlangPatterns =
    {
        new Regex(@"\by['’]all\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\bdude\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\bain['’]?t\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\bawesome\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\bgonna\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\bwanna\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    private readonly AppDbContext _db;

    public VoicePackPolicyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<VoicePackPolicyDecision> EvaluateAsync(
        Guid campaignId,
        Guid? voicePackId,
        VoicePackPolicyInput input,
        CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Where(item => item.Id == campaignId)
            .Select(item => new
            {
                item.Id,
                item.UserId,
                SelectedBudget = item.PackageOrder.SelectedBudget ?? item.PackageOrder.Amount
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        AiVoicePack? pack = null;
        if (voicePackId.HasValue)
        {
            pack = await _db.AiVoicePacks
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.Id == voicePackId.Value
                            && item.IsActive
                            && (!item.IsClientSpecific || item.ClientUserId == campaign.UserId),
                    cancellationToken)
                ?? throw new InvalidOperationException("Voice pack not found, inactive, or not assigned to this client.");
        }

        var requestedLanguage = NormalizeLanguage(input.RequestedLanguage);
        var packLanguage = NormalizeLanguage(pack?.Language);
        var appliedLanguage = ResolveLanguage(requestedLanguage, packLanguage);

        var effectiveBudget = input.PackageBudget ?? campaign.SelectedBudget;
        var allowedRank = GetAllowedTierRank(effectiveBudget, input.CampaignTier);
        var packTierRank = GetTierRank(pack?.PricingTier ?? "standard");
        var upsellRequired = packTierRank > allowedRank;
        var upsellMessage = upsellRequired
            ? $"Selected voice pack tier '{pack?.PricingTier ?? "standard"}' exceeds this package allowance. Upgrade package or approve AI upsell."
            : null;
        if (upsellRequired && !input.AllowTierUpsell)
        {
            throw new InvalidOperationException(upsellMessage);
        }

        var moderation = EvaluateModeration(pack, input.Script, appliedLanguage);
        var qa = ScoreQa(pack, input.Script, appliedLanguage, input.Objective);

        return new VoicePackPolicyDecision(
            AppliedLanguage: appliedLanguage,
            UpsellRequired: upsellRequired,
            UpsellMessage: upsellMessage,
            Moderation: moderation,
            QaScore: qa);
    }

    public async Task<VoicePackRecommendationResult?> RecommendAsync(
        Guid campaignId,
        string provider,
        string? audience,
        string? objective,
        decimal? packageBudget,
        string? campaignTier,
        CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Where(item => item.Id == campaignId)
            .Select(item => new
            {
                item.UserId,
                SelectedBudget = item.PackageOrder.SelectedBudget ?? item.PackageOrder.Amount
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var budget = packageBudget ?? campaign.SelectedBudget;
        var allowedRank = GetAllowedTierRank(budget, campaignTier);
        var audienceTokens = Tokenize(audience);
        var objectiveTokens = Tokenize(objective);

        var packs = await _db.AiVoicePacks
            .AsNoTracking()
            .Where(item => item.Provider == provider && item.IsActive)
            .Where(item => !item.IsClientSpecific || item.ClientUserId == campaign.UserId)
            .ToArrayAsync(cancellationToken);

        var scored = packs
            .Where(item => GetTierRank(item.PricingTier) <= allowedRank)
            .Select(item =>
            {
                var audienceTags = DeserializeList(item.AudienceTagsJson);
                var objectiveTags = DeserializeList(item.ObjectiveTagsJson);
                decimal score = 0m;

                score += OverlapScore(audienceTokens, audienceTags, 4m);
                score += OverlapScore(objectiveTokens, objectiveTags, 3m);
                score += item.IsClonedVoice ? 1.2m : 0.4m;
                score += string.Equals(NormalizeLanguage(item.Language), "zulu", StringComparison.Ordinal) ? 0.3m : 0m;
                score += Math.Max(0m, 1m - (item.SortOrder * 0.05m));

                var reason = $"Audience match {OverlapCount(audienceTokens, audienceTags)} | Objective match {OverlapCount(objectiveTokens, objectiveTags)} | Tier {item.PricingTier}";
                return new { item.Id, Score = Math.Round(score, 2), Reason = reason };
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Id)
            .FirstOrDefault();

        return scored is null
            ? null
            : new VoicePackRecommendationResult(scored.Id, scored.Reason, scored.Score);
    }

    private static VoicePackModerationResult EvaluateModeration(AiVoicePack? pack, string script, string appliedLanguage)
    {
        var flags = new List<string>();
        var suggestions = new List<string>();
        var isSaLocalPack = IsSouthAfricanPack(pack, appliedLanguage);
        if (isSaLocalPack)
        {
            foreach (var pattern in UsSlangPatterns)
            {
                if (!pattern.IsMatch(script))
                {
                    continue;
                }

                flags.Add($"US slang blocked: `{pattern}`");
            }
        }

        if (flags.Count > 0)
        {
            suggestions.Add("Replace US slang with South African phrasing.");
            suggestions.Add("Use local references (Mzansi, kasi, local suburb naming) where appropriate.");
        }

        return new VoicePackModerationResult(flags.Count == 0, flags.ToArray(), suggestions.ToArray());
    }

    private static VoicePackQaScore ScoreQa(AiVoicePack? pack, string script, string appliedLanguage, string? objective)
    {
        var notes = new List<string>();
        var wordCount = script.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        var hasCta = ContainsAny(script, "call", "visit", "book", "sign up", "register", "today", "now");
        var hasSaCue = ContainsAny(script, "south africa", "mzansi", "kasi", "johannesburg", "cape town", "durban", "stellenbosch");

        decimal authenticity = 5.0m;
        if (IsSouthAfricanPack(pack, appliedLanguage))
        {
            authenticity += 2.0m;
            notes.Add("SA-local voice pack selected.");
        }

        if (hasSaCue)
        {
            authenticity += 1.5m;
            notes.Add("Script includes local SA cue words.");
        }

        if (ContainsAny(script, "y'all", "dude", "ain't", "awesome"))
        {
            authenticity -= 2.0m;
            notes.Add("US slang patterns reduce authenticity.");
        }

        decimal clarity = wordCount <= 40 ? 8.8m : wordCount <= 60 ? 7.4m : 6.0m;
        if (wordCount <= 25)
        {
            notes.Add("Concise script suited for short-form radio.");
        }

        decimal conversion = hasCta ? 8.6m : 6.1m;
        if (!string.IsNullOrWhiteSpace(objective) && hasCta)
        {
            conversion += 0.4m;
            notes.Add("CTA present and objective-aware.");
        }

        return new VoicePackQaScore(
            Clamp(authenticity),
            Clamp(clarity),
            Clamp(conversion),
            notes.ToArray());
    }

    private static string ResolveLanguage(string? requestedLanguage, string? packLanguage)
    {
        if (!string.IsNullOrWhiteSpace(requestedLanguage) && SaLanguages.Contains(requestedLanguage, StringComparer.OrdinalIgnoreCase))
        {
            return requestedLanguage;
        }

        if (!string.IsNullOrWhiteSpace(packLanguage))
        {
            return packLanguage;
        }

        return "english";
    }

    private static bool IsSouthAfricanPack(AiVoicePack? pack, string appliedLanguage)
    {
        if (pack is null)
        {
            return SaLanguages.Contains(appliedLanguage, StringComparer.OrdinalIgnoreCase);
        }

        var pool = string.Join(
            " ",
            new[]
            {
                pack.Name,
                pack.Accent ?? string.Empty,
                pack.Language ?? string.Empty,
                pack.Tone ?? string.Empty,
                pack.Persona ?? string.Empty
            });

        return ContainsAny(pool, "south african", "mzansi", "kasi", "afrikaans", "zulu", "xhosa")
               || SaLanguages.Contains(NormalizeLanguage(pack.Language), StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeLanguage(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "english" : value.Trim().ToLowerInvariant();
    }

    private static decimal Clamp(decimal value)
    {
        return Math.Min(10m, Math.Max(0m, Math.Round(value, 2)));
    }

    private static bool ContainsAny(string source, params string[] terms)
    {
        return terms.Any(term => source.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetTierRank(string tier)
    {
        return tier.Trim().ToLowerInvariant() switch
        {
            "exclusive" => 3,
            "premium" => 2,
            _ => 1
        };
    }

    private static int GetAllowedTierRank(decimal packageBudget, string? campaignTier)
    {
        var fromTier = (campaignTier ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "exclusive" => 3,
            "premium" => 2,
            "standard" => 1,
            _ => 0
        };

        var fromBudget = packageBudget switch
        {
            < 50000m => 1,
            < 150000m => 2,
            _ => 3
        };

        return Math.Max(fromTier, fromBudget);
    }

    private static HashSet<string> Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return value
            .Split(new[] { ',', ';', ' ', '/', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length >= 3)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static decimal OverlapScore(HashSet<string> tokens, string[] tags, decimal maxScore)
    {
        if (tokens.Count == 0 || tags.Length == 0)
        {
            return 0m;
        }

        var count = OverlapCount(tokens, tags);
        if (count == 0)
        {
            return 0m;
        }

        var ratio = Math.Min(1m, count / (decimal)Math.Max(1, tags.Length));
        return Math.Round(maxScore * ratio, 2);
    }

    private static int OverlapCount(HashSet<string> tokens, string[] tags)
    {
        return tags.Count(tag => tokens.Contains(tag));
    }

    private static string[] DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
