using System.Text.Json;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class RuleBasedCreativeValidationService : ICreativeValidationService
{
    public CreativeValidationResult Validate(CreativeVariant creative)
    {
        var errors = new List<string>();
        using var document = ParseJson(creative.PayloadJson);
        var content = ExtractContent(document);

        if (creative.Channel == AdvertisingChannel.Radio)
        {
            var cta = GetString(content, "cta");
            if (string.IsNullOrWhiteSpace(cta))
            {
                errors.Add("Radio creative must include CTA.");
            }

            var duration = GetInt(content, "durationSeconds") ?? GetInt(content, "duration") ?? 30;
            if (duration < 20 || duration > 40)
            {
                errors.Add("Radio duration should be approximately 30 seconds.");
            }

            var structure = GetStringArray(content, "structure");
            if (!structure.Any(item => item.Equals("hook", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("Radio creative should include a hook structure element.");
            }
        }

        if (creative.Channel == AdvertisingChannel.Billboard)
        {
            var headline = GetString(content, "headline");
            if (string.IsNullOrWhiteSpace(headline))
            {
                errors.Add("Billboard headline is required.");
            }
            else
            {
                var words = headline.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 6)
                {
                    errors.Add("Billboard headline exceeds 6 words.");
                }

                if (headline.Count(ch => ch == '!') > 1 || headline.Count(ch => ch == '?') > 1)
                {
                    errors.Add("Billboard headline has punctuation overload.");
                }
            }
        }

        if (creative.Channel == AdvertisingChannel.Digital)
        {
            var headline = GetString(content, "headline");
            if (!string.IsNullOrWhiteSpace(headline) && headline.Length > 60)
            {
                errors.Add("Digital headline exceeds recommended length.");
            }

            var payload = creative.PayloadJson.ToLowerInvariant();
            var bannedWords = new[] { "guaranteed millions", "instant rich", "cure all" };
            foreach (var banned in bannedWords)
            {
                if (payload.Contains(banned, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Digital creative contains banned wording: '{banned}'.");
                }
            }
        }

        return new CreativeValidationResult(errors.Count == 0, errors);
    }

    private static JsonDocument ParseJson(string payloadJson)
    {
        try
        {
            return JsonDocument.Parse(payloadJson);
        }
        catch
        {
            return JsonDocument.Parse("{}");
        }
    }

    private static JsonElement ExtractContent(JsonDocument document)
    {
        var root = document.RootElement;
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object)
        {
            return content;
        }

        return root;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? parsed : null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }
}

public sealed class HybridCreativeScoringService : ICreativeScoringService
{
    private readonly IMultiAiProviderOrchestrator _orchestrator;

    public HybridCreativeScoringService(IMultiAiProviderOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<CreativeScoreResult> ScoreAsync(CreativeBrief brief, CreativeVariant creative, CancellationToken cancellationToken)
    {
        var scorePrompt = JsonSerializer.Serialize(new
        {
            systemPrompt = "You are an expert advertising reviewer.",
            userPrompt =
                $"Score this ad from 0-10 for clarity, attention, emotionalImpact, ctaStrength, brandFit, channelFit. " +
                $"Return JSON with issues and suggestions. Channel: {creative.Channel}, Language: {creative.Language}. Payload: {creative.PayloadJson}",
            outputSchemaJson = "{\"clarity\":0,\"attention\":0,\"emotionalImpact\":0,\"ctaStrength\":0,\"brandFit\":0,\"channelFit\":0,\"issues\":[],\"suggestions\":[]}",
            channel = creative.Channel.ToString(),
            language = creative.Language,
            templateKey = "creative-qa-default"
        });

        var response = await _orchestrator.ExecuteAsync(creative.Channel, "creative-qa", scorePrompt, cancellationToken);
        var parsed = TryParseScore(response);

        var clarity = parsed.TryGetValue("clarity", out var clarityScore) ? clarityScore : 8.0m;
        var attention = parsed.TryGetValue("attention", out var attentionScore) ? attentionScore : 8.0m;
        var emotionalImpact = parsed.TryGetValue("emotionalImpact", out var emotionalImpactScore) ? emotionalImpactScore : 8.0m;
        var ctaStrength = parsed.TryGetValue("ctaStrength", out var ctaScore) ? ctaScore : 8.0m;
        var brandFit = parsed.TryGetValue("brandFit", out var brandFitScore) ? brandFitScore : 8.0m;
        var channelFit = parsed.TryGetValue("channelFit", out var channelFitScore) ? channelFitScore : 8.0m;

        var final = Math.Round(
            (clarity * 0.2m) +
            (attention * 0.2m) +
            (emotionalImpact * 0.2m) +
            (ctaStrength * 0.2m) +
            (channelFit * 0.2m),
            2);

        return new CreativeScoreResult(
            clarity,
            attention,
            emotionalImpact,
            ctaStrength,
            brandFit,
            channelFit,
            final,
            ExtractStringArray(response, "issues"),
            ExtractStringArray(response, "suggestions"));
    }

    private static Dictionary<string, decimal> TryParseScore(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in new[] { "clarity", "attention", "emotionalImpact", "ctaStrength", "brandFit", "channelFit" })
            {
                if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var parsed))
                {
                    map[key] = Clamp(parsed);
                }
            }

            return map;
        }
        catch
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<string> ExtractStringArray(string responseJson, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return arr.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static decimal Clamp(decimal score) => Math.Min(10.0m, Math.Max(0.0m, score));
}

public sealed class ComplianceCreativeRiskService : ICreativeRiskService
{
    public CreativeRiskResult Analyze(CreativeVariant creative)
    {
        var flags = new List<string>();
        var payload = creative.PayloadJson.ToLowerInvariant();

        if (payload.Contains("best in sa"))
        {
            flags.Add("Unverified claim: 'best in SA'.");
        }

        if (payload.Contains("guaranteed returns") || payload.Contains("guaranteed profit"))
        {
            flags.Add("Potential misleading financial promise.");
        }

        if (payload.Contains("cure"))
        {
            flags.Add("Potential unsubstantiated health claim.");
        }

        if (payload.Contains("political"))
        {
            flags.Add("Potential political sensitivity.");
        }

        if (payload.Contains("stupid") || payload.Contains("idiot"))
        {
            flags.Add("Brand safety issue: offensive language.");
        }

        var riskLevel = flags.Count switch
        {
            0 => "Low",
            <= 2 => "Medium",
            _ => "High"
        };

        var action = riskLevel switch
        {
            "Low" => "Allow",
            "Medium" => "Modify",
            _ => "Block"
        };

        return new CreativeRiskResult(riskLevel, flags, action);
    }
}

public sealed class CreativeImprovementService : ICreativeImprovementService
{
    public Task<CreativeImprovementResult?> ImproveAsync(
        CreativeBrief brief,
        CreativeVariant creative,
        IReadOnlyList<string> issues,
        IReadOnlyList<string> suggestions,
        CancellationToken cancellationToken)
    {
        if (issues.Count == 0 && suggestions.Count == 0)
        {
            return Task.FromResult<CreativeImprovementResult?>(null);
        }

        var changes = new List<string>();
        if (issues.Any(item => item.Contains("cta", StringComparison.OrdinalIgnoreCase)))
        {
            changes.Add("Strengthened CTA language.");
        }

        if (issues.Any(item => item.Contains("emotional", StringComparison.OrdinalIgnoreCase)))
        {
            changes.Add("Increased emotional resonance.");
        }

        if (changes.Count == 0)
        {
            changes.Add("Refined messaging for clarity and channel fit.");
        }

        var improvedJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            channel = creative.Channel.ToString(),
            language = creative.Language,
            updatedAd = creative.PayloadJson,
            changes
        });

        return Task.FromResult<CreativeImprovementResult?>(new CreativeImprovementResult(improvedJson, changes));
    }
}

public sealed class ThresholdCreativeDecisionService : ICreativeDecisionService
{
    public string Decide(decimal finalScore, string riskLevel)
    {
        if (string.Equals(riskLevel, "High", StringComparison.OrdinalIgnoreCase))
        {
            return "Rejected";
        }

        if (finalScore >= 8.5m)
        {
            return "Approved";
        }

        if (finalScore < 6.0m)
        {
            return "Rejected";
        }

        return "NeedsImprovement";
    }
}

public sealed class DbCreativeQaResultRepository : ICreativeQaResultRepository
{
    private readonly AppDbContext _db;

    public DbCreativeQaResultRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(IReadOnlyList<CreativeQaResult> results, CancellationToken cancellationToken)
    {
        foreach (var result in results)
        {
            _db.AiCreativeQaResults.Add(new AiCreativeQaResult
            {
                Id = Guid.NewGuid(),
                CreativeId = result.CreativeId,
                CampaignId = result.CampaignId,
                Channel = result.Channel.ToString(),
                Language = result.Language,
                Clarity = result.Clarity,
                Attention = result.Attention,
                EmotionalImpact = result.EmotionalImpact,
                CtaStrength = result.CtaStrength,
                BrandFit = result.BrandFit,
                ChannelFit = result.ChannelFit,
                FinalScore = result.FinalScore,
                Status = result.Status,
                RiskLevel = result.RiskLevel,
                IssuesJson = JsonSerializer.Serialize(result.Issues),
                SuggestionsJson = JsonSerializer.Serialize(result.Suggestions),
                RiskFlagsJson = JsonSerializer.Serialize(result.RiskFlags),
                ImprovedPayloadJson = result.ImprovedPayloadJson,
                CreatedAt = result.CreatedAt.UtcDateTime
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CreativeQaResult>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var rows = await _db.AiCreativeQaResults
            .AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(item =>
            new CreativeQaResult(
                item.CreativeId,
                item.CampaignId,
                Enum.TryParse<AdvertisingChannel>(item.Channel, true, out var channel) ? channel : AdvertisingChannel.Digital,
                item.Language,
                item.Clarity,
                item.Attention,
                item.EmotionalImpact,
                item.CtaStrength,
                item.BrandFit,
                item.ChannelFit,
                item.FinalScore,
                item.Status,
                item.RiskLevel,
                DeserializeStringList(item.IssuesJson),
                DeserializeStringList(item.SuggestionsJson),
                DeserializeStringList(item.RiskFlagsJson),
                item.ImprovedPayloadJson,
                new DateTimeOffset(item.CreatedAt, TimeSpan.Zero)))
            .ToArray();
    }

    private static IReadOnlyList<string> DeserializeStringList(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(json) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

public sealed class PipelineCreativeQaService : ICreativeQaService
{
    private readonly ICreativeValidationService _validationService;
    private readonly ICreativeScoringService _scoringService;
    private readonly ICreativeRiskService _riskService;
    private readonly ICreativeImprovementService _improvementService;
    private readonly ICreativeDecisionService _decisionService;
    private readonly ICreativeQaResultRepository _qaResultRepository;

    public PipelineCreativeQaService(
        ICreativeValidationService validationService,
        ICreativeScoringService scoringService,
        ICreativeRiskService riskService,
        ICreativeImprovementService improvementService,
        ICreativeDecisionService decisionService,
        ICreativeQaResultRepository qaResultRepository)
    {
        _validationService = validationService;
        _scoringService = scoringService;
        _riskService = riskService;
        _improvementService = improvementService;
        _decisionService = decisionService;
        _qaResultRepository = qaResultRepository;
    }

    public async Task<IReadOnlyList<CreativeQualityScore>> ScoreAsync(
        CreativeBrief brief,
        IReadOnlyList<CreativeVariant> creatives,
        CancellationToken cancellationToken)
    {
        var results = new List<CreativeQualityScore>();
        var qaRows = new List<CreativeQaResult>();

        foreach (var creative in creatives)
        {
            var validation = _validationService.Validate(creative);
            if (!validation.IsValid)
            {
                var rejectedScore = new CreativeQualityScore(
                    creative.CreativeId,
                    creative.Channel,
                    new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["clarity"] = 0,
                        ["attention"] = 0,
                        ["emotionalImpact"] = 0,
                        ["ctaStrength"] = 0,
                        ["brandFit"] = 0,
                        ["channelFit"] = 0
                    },
                    0,
                    "Rejected",
                    "Medium",
                    validation.Errors,
                    new[] { "Fix validation errors and regenerate." });

                results.Add(rejectedScore);
                qaRows.Add(new CreativeQaResult(
                    creative.CreativeId,
                    creative.CampaignId,
                    creative.Channel,
                    creative.Language,
                    0, 0, 0, 0, 0, 0,
                    0,
                    "Rejected",
                    "Medium",
                    validation.Errors,
                    new[] { "Fix validation errors and regenerate." },
                    Array.Empty<string>(),
                    null,
                    DateTimeOffset.UtcNow));
                continue;
            }

            var score = await _scoringService.ScoreAsync(brief, creative, cancellationToken);
            var risk = _riskService.Analyze(creative);
            var status = _decisionService.Decide(score.FinalScore, risk.RiskLevel);
            var improvement = status == "Approved"
                ? null
                : await _improvementService.ImproveAsync(brief, creative, score.Issues, score.Suggestions, cancellationToken);

            var issues = score.Issues.Concat(risk.Flags).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var suggestions = improvement?.Changes ?? score.Suggestions;

            var metrics = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["clarity"] = score.Clarity,
                ["attention"] = score.Attention,
                ["emotionalImpact"] = score.EmotionalImpact,
                ["ctaStrength"] = score.CtaStrength,
                ["brandFit"] = score.BrandFit,
                ["channelFit"] = score.ChannelFit
            };

            results.Add(new CreativeQualityScore(
                creative.CreativeId,
                creative.Channel,
                metrics,
                score.FinalScore,
                status,
                risk.RiskLevel,
                issues,
                suggestions));

            qaRows.Add(new CreativeQaResult(
                creative.CreativeId,
                creative.CampaignId,
                creative.Channel,
                creative.Language,
                score.Clarity,
                score.Attention,
                score.EmotionalImpact,
                score.CtaStrength,
                score.BrandFit,
                score.ChannelFit,
                score.FinalScore,
                status,
                risk.RiskLevel,
                issues,
                suggestions,
                risk.Flags,
                improvement?.UpdatedAdJson,
                DateTimeOffset.UtcNow));
        }

        await _qaResultRepository.SaveAsync(qaRows, cancellationToken);
        return results;
    }
}
