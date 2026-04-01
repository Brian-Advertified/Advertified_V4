using System.Text.Json;
using Advertified.App.Contracts.Creatives;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class CreativeGenerationOrchestrator : ICreativeGenerationOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;

    public CreativeGenerationOrchestrator(AppDbContext db)
    {
        _db = db;
    }

    public async Task<GenerateCreativesResponse> GenerateAsync(
        GenerateCreativesRequest request,
        Guid? sourceCreativeSystemId,
        bool persistOutputs,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(request);
        var brief = BuildCreativeBrief(normalized);
        var runId = Guid.NewGuid().ToString("N");
        var warnings = new List<string>();

        var creatives = new GeneratedCreativesByChannelResponse
        {
            Radio = normalized.Channels.Contains("radio", StringComparer.OrdinalIgnoreCase)
                ? GenerateRadio(brief, normalized.Audience.Languages)
                : Array.Empty<RadioCreativeResponse>(),
            Tv = normalized.Channels.Contains("tv", StringComparer.OrdinalIgnoreCase)
                ? GenerateTv(brief)
                : Array.Empty<TvCreativeResponse>(),
            Billboard = normalized.Channels.Contains("billboard", StringComparer.OrdinalIgnoreCase)
                ? GenerateBillboard(brief)
                : Array.Empty<BillboardCreativeResponse>(),
            Newspaper = normalized.Channels.Contains("newspaper", StringComparer.OrdinalIgnoreCase)
                ? GenerateNewspaper(brief)
                : Array.Empty<NewspaperCreativeResponse>(),
            Digital = normalized.Channels.Contains("digital", StringComparer.OrdinalIgnoreCase)
                ? GenerateDigital(brief)
                : Array.Empty<DigitalCreativeResponse>()
        };

        var scores = ScoreCreatives(creatives);
        if (persistOutputs)
        {
            await PersistAsync(normalized.CampaignId, sourceCreativeSystemId, creatives, scores, cancellationToken);
        }

        return new GenerateCreativesResponse
        {
            CampaignId = normalized.CampaignId,
            Creatives = creatives,
            Scores = scores,
            Metadata = new CreativeGenerationMetadataResponse
            {
                RunId = runId,
                BriefVersion = "v1",
                GeneratorVersion = "v1",
                GeneratedAt = DateTimeOffset.UtcNow,
                Warnings = warnings
            }
        };
    }

    public async Task<GenerateCreativesResponse> RegenerateAsync(
        RegenerateCreativeRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _db.CampaignCreatives
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CreativeId, cancellationToken)
            ?? throw new InvalidOperationException("Creative not found.");

        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.CampaignBrief)
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == existing.CampaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var requestModel = new GenerateCreativesRequest
        {
            CampaignId = campaign.Id.ToString(),
            Business = new CreativeBusinessRequest
            {
                Name = campaign.User.BusinessProfile?.BusinessName ?? campaign.User.FullName,
                Industry = campaign.User.BusinessProfile?.Industry ?? "General",
                Location = campaign.User.BusinessProfile?.City ?? campaign.User.BusinessProfile?.Province ?? "South Africa"
            },
            Objective = campaign.CampaignBrief?.Objective ?? "Awareness",
            Budget = campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
            Audience = new CreativeAudienceRequest
            {
                Lsm = $"{campaign.CampaignBrief?.TargetLsmMin ?? 4}-{campaign.CampaignBrief?.TargetLsmMax ?? 8}",
                AgeRange = $"{campaign.CampaignBrief?.TargetAgeMin ?? 25}-{campaign.CampaignBrief?.TargetAgeMax ?? 45}",
                Languages = campaign.CampaignBrief?.GetList(nameof(Advertified.App.Data.Entities.CampaignBrief.TargetLanguagesJson))?.ToArray() ?? new[] { "English" }
            },
            Channels = new[] { existing.Channel },
            Tone = string.IsNullOrWhiteSpace(request.Feedback)
                ? "Balanced"
                : $"Balanced | {request.Feedback.Trim()}"
        };

        return await GenerateAsync(requestModel, existing.SourceCreativeSystemId, true, cancellationToken);
    }

    public async Task<GenerateCreativesRequest> BuildNormalizedRequestFromCampaignAsync(
        Guid campaignId,
        string prompt,
        string? objective,
        string? tone,
        IReadOnlyList<string>? channels,
        CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.CampaignBrief)
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var inferredChannels = channels?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
            ?? InferChannelsFromPrompt(prompt);

        return new GenerateCreativesRequest
        {
            CampaignId = campaign.Id.ToString(),
            Business = new CreativeBusinessRequest
            {
                Name = campaign.User.BusinessProfile?.BusinessName ?? campaign.User.FullName,
                Industry = campaign.User.BusinessProfile?.Industry ?? "General",
                Location = campaign.User.BusinessProfile?.City ?? campaign.User.BusinessProfile?.Province ?? "South Africa"
            },
            Objective = string.IsNullOrWhiteSpace(objective)
                ? campaign.CampaignBrief?.Objective ?? "Awareness"
                : objective.Trim(),
            Budget = campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
            Audience = new CreativeAudienceRequest
            {
                Lsm = $"{campaign.CampaignBrief?.TargetLsmMin ?? 4}-{campaign.CampaignBrief?.TargetLsmMax ?? 8}",
                AgeRange = $"{campaign.CampaignBrief?.TargetAgeMin ?? 25}-{campaign.CampaignBrief?.TargetAgeMax ?? 45}",
                Languages = campaign.CampaignBrief?.GetList(nameof(Advertified.App.Data.Entities.CampaignBrief.TargetLanguagesJson))?.ToArray() ?? new[] { "English" }
            },
            Channels = inferredChannels,
            Tone = string.IsNullOrWhiteSpace(tone) ? "Balanced" : tone.Trim()
        };
    }

    public LocalisationResponse Localize(LocalisationRequest request)
    {
        var adapted = request.Content.Trim();
        if (!string.Equals(request.BaseLanguage, request.TargetLanguage, StringComparison.OrdinalIgnoreCase))
        {
            adapted = $"[{request.TargetLanguage} adaptation | {request.Tone}] {adapted}";
        }

        return new LocalisationResponse
        {
            BaseLanguage = request.BaseLanguage,
            TargetLanguage = request.TargetLanguage,
            AdaptedContent = adapted,
            Notes = new[]
            {
                "Tone and audience intent preserved.",
                "Localization keeps CTA direct and culturally familiar."
            }
        };
    }

    private static GenerateCreativesRequest NormalizeRequest(GenerateCreativesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CampaignId))
        {
            throw new InvalidOperationException("campaignId is required.");
        }

        if (request.Budget <= 0)
        {
            throw new InvalidOperationException("budget must be greater than zero.");
        }

        if (request.Channels.Count == 0)
        {
            throw new InvalidOperationException("At least one channel is required.");
        }

        request.Business.Name = request.Business.Name.Trim();
        request.Business.Industry = request.Business.Industry.Trim();
        request.Business.Location = request.Business.Location.Trim();
        request.Objective = request.Objective.Trim();
        request.Tone = request.Tone.Trim();
        request.Audience.Lsm = request.Audience.Lsm.Trim();
        request.Audience.AgeRange = request.Audience.AgeRange.Trim();
        request.Audience.Languages = request.Audience.Languages
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeLanguage)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        request.Channels = request.Channels
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeChannel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (request.Audience.Languages.Count == 0)
        {
            request.Audience.Languages = new[] { "English" };
        }

        return request;
    }

    private static NormalizedCreativeBrief BuildCreativeBrief(GenerateCreativesRequest request)
    {
        return new NormalizedCreativeBrief(
            Brand: request.Business.Name,
            Industry: request.Business.Industry,
            Location: request.Business.Location,
            Objective: HumanizeObjective(request.Objective),
            Tone: request.Tone,
            KeyMessage: $"Fast, reliable {request.Business.Industry.ToLowerInvariant()} offering near {request.Business.Location}.",
            Cta: BuildCtaForObjective(request.Objective),
            AudienceInsights: new[]
            {
                $"LSM {request.Audience.Lsm}",
                $"Age {request.Audience.AgeRange}",
                request.Business.Location
            },
            Languages: request.Audience.Languages
        );
    }

    private static IReadOnlyList<RadioCreativeResponse> GenerateRadio(NormalizedCreativeBrief brief, IReadOnlyList<string> languages)
    {
        return languages.Select(language => new RadioCreativeResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            Language = language,
            Duration = 30,
            Script = $"[Hook] {brief.Brand} is built for {brief.AudienceInsights[2]}. [Body] {brief.KeyMessage} [CTA] {brief.Cta}",
            VoiceTone = brief.Tone,
            Cta = brief.Cta,
            Format = "Dialogue"
        }).ToArray();
    }

    private static IReadOnlyList<TvCreativeResponse> GenerateTv(NormalizedCreativeBrief brief)
    {
        return new[]
        {
            new TvCreativeResponse
            {
                Id = Guid.NewGuid().ToString("N"),
                Duration = 30,
                Cta = brief.Cta,
                Scenes = new[]
                {
                    new TvSceneResponse { Scene = 1, Description = "Daily pain point is shown in urban commute context.", Dialogue = $"People need {brief.Brand} now." },
                    new TvSceneResponse { Scene = 2, Description = "Brand demonstrates clear product value in a fast visual sequence.", Dialogue = brief.KeyMessage },
                    new TvSceneResponse { Scene = 3, Description = "Closing logo lockup with CTA and location cue.", Dialogue = brief.Cta }
                }
            }
        };
    }

    private static IReadOnlyList<BillboardCreativeResponse> GenerateBillboard(NormalizedCreativeBrief brief)
    {
        return new[]
        {
            new BillboardCreativeResponse
            {
                Id = Guid.NewGuid().ToString("N"),
                Headline = $"{brief.Brand}. No Delay.",
                Subtext = $"{brief.KeyMessage}",
                Cta = brief.Cta,
                VisualDirection = $"High-contrast product moment, clean background, location cue: {brief.Location}."
            }
        };
    }

    private static IReadOnlyList<NewspaperCreativeResponse> GenerateNewspaper(NormalizedCreativeBrief brief)
    {
        return new[]
        {
            new NewspaperCreativeResponse
            {
                Id = Guid.NewGuid().ToString("N"),
                Headline = $"{brief.Brand} for {brief.Location}",
                Body = $"{brief.KeyMessage} Built for {string.Join(", ", brief.AudienceInsights)}.",
                Cta = brief.Cta
            }
        };
    }

    private static IReadOnlyList<DigitalCreativeResponse> GenerateDigital(NormalizedCreativeBrief brief)
    {
        return new[]
        {
            new DigitalCreativeResponse
            {
                Id = Guid.NewGuid().ToString("N"),
                Platform = "Meta",
                PrimaryText = brief.KeyMessage,
                Headline = $"{brief.Brand} near you",
                Cta = brief.Cta,
                Variants = 3
            },
            new DigitalCreativeResponse
            {
                Id = Guid.NewGuid().ToString("N"),
                Platform = "TikTok",
                PrimaryText = brief.KeyMessage,
                Headline = $"{brief.Brand} now",
                Hook = "Stop scrolling.",
                Script = $"{brief.Brand} solves this fast. {brief.Cta}",
                Duration = 15,
                Cta = brief.Cta,
                Variants = 3
            }
        };
    }

    private static CreativeScoresResponse ScoreCreatives(GeneratedCreativesByChannelResponse creatives)
    {
        return new CreativeScoresResponse
        {
            Radio = creatives.Radio.Select(item => Score(item.Id, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["clarity"] = 8.6m,
                ["emotionalImpact"] = 7.9m,
                ["ctaStrength"] = 8.9m
            })).ToArray(),
            Tv = creatives.Tv.Select(item => Score(item.Id, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["narrativeFlow"] = 8.4m,
                ["brandRecall"] = 8.1m,
                ["ctaStrength"] = 8.7m
            })).ToArray(),
            Billboard = creatives.Billboard.Select(item => Score(item.Id, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["readability"] = 9.1m,
                ["attention"] = 8.8m,
                ["ctaStrength"] = 8.5m
            })).ToArray(),
            Newspaper = creatives.Newspaper.Select(item => Score(item.Id, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["readability"] = 8.2m,
                ["messageDepth"] = 8.0m,
                ["ctaStrength"] = 8.3m
            })).ToArray(),
            Digital = creatives.Digital.Select(item => Score(item.Id, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["hookStrength"] = 8.7m,
                ["platformFit"] = 8.6m,
                ["ctaStrength"] = 8.8m
            })).ToArray()
        };
    }

    private async Task PersistAsync(
        string campaignId,
        Guid? sourceCreativeSystemId,
        GeneratedCreativesByChannelResponse creatives,
        CreativeScoresResponse scores,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(campaignId, out var parsedCampaignId))
        {
            return;
        }

        var campaignExists = await _db.Campaigns.AnyAsync(x => x.Id == parsedCampaignId, cancellationToken);
        if (!campaignExists)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var rows = new List<CampaignCreative>();

        rows.AddRange(creatives.Radio.Select(item => ToCreativeRow(parsedCampaignId, sourceCreativeSystemId, "radio", item.Language, "radio_script", item.Id, item, scores.Radio)));
        rows.AddRange(creatives.Tv.Select(item => ToCreativeRow(parsedCampaignId, sourceCreativeSystemId, "tv", "English", "tv_storyboard", item.Id, item, scores.Tv)));
        rows.AddRange(creatives.Billboard.Select(item => ToCreativeRow(parsedCampaignId, sourceCreativeSystemId, "billboard", "English", "billboard_copy", item.Id, item, scores.Billboard)));
        rows.AddRange(creatives.Newspaper.Select(item => ToCreativeRow(parsedCampaignId, sourceCreativeSystemId, "newspaper", "English", "newspaper_copy", item.Id, item, scores.Newspaper)));
        rows.AddRange(creatives.Digital.Select(item => ToCreativeRow(parsedCampaignId, sourceCreativeSystemId, "digital", "English", "digital_variant", item.Id, item, scores.Digital)));

        foreach (var row in rows)
        {
            row.CreatedAt = now;
            row.UpdatedAt = now;
        }

        _db.CampaignCreatives.AddRange(rows);
        await _db.SaveChangesAsync(cancellationToken);

        var scoreRows = new List<CreativeScore>();
        foreach (var row in rows)
        {
            var score = GetScoreMap(row, scores);
            foreach (var metric in score)
            {
                scoreRows.Add(new CreativeScore
                {
                    Id = Guid.NewGuid(),
                    CampaignCreativeId = row.Id,
                    MetricName = metric.Key,
                    MetricValue = metric.Value,
                    CreatedAt = now
                });
            }
        }

        if (scoreRows.Count > 0)
        {
            _db.CreativeScores.AddRange(scoreRows);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static CampaignCreative ToCreativeRow<T>(
        Guid campaignId,
        Guid? sourceCreativeSystemId,
        string channel,
        string language,
        string creativeType,
        string externalId,
        T payload,
        IReadOnlyList<CreativeChannelScoreResponse> channelScores)
    {
        var score = channelScores
            .FirstOrDefault(item => string.Equals(item.CreativeId, externalId, StringComparison.OrdinalIgnoreCase))?
            .Metrics
            .Values
            .DefaultIfEmpty(0m)
            .Average();

        return new CampaignCreative
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            SourceCreativeSystemId = sourceCreativeSystemId,
            Channel = channel,
            Language = language,
            CreativeType = creativeType,
            JsonPayload = JsonSerializer.Serialize(payload, JsonOptions),
            Score = !score.HasValue || score.Value == 0m ? null : Math.Round(score.Value, 2)
        };
    }

    private static IReadOnlyDictionary<string, decimal> GetScoreMap(CampaignCreative row, CreativeScoresResponse scores)
    {
        return row.Channel switch
        {
            "radio" => scores.Radio.FirstOrDefault(x => x.CreativeId == ExtractCreativeId(row.JsonPayload))?.Metrics ?? new Dictionary<string, decimal>(),
            "tv" => scores.Tv.FirstOrDefault(x => x.CreativeId == ExtractCreativeId(row.JsonPayload))?.Metrics ?? new Dictionary<string, decimal>(),
            "billboard" => scores.Billboard.FirstOrDefault(x => x.CreativeId == ExtractCreativeId(row.JsonPayload))?.Metrics ?? new Dictionary<string, decimal>(),
            "newspaper" => scores.Newspaper.FirstOrDefault(x => x.CreativeId == ExtractCreativeId(row.JsonPayload))?.Metrics ?? new Dictionary<string, decimal>(),
            "digital" => scores.Digital.FirstOrDefault(x => x.CreativeId == ExtractCreativeId(row.JsonPayload))?.Metrics ?? new Dictionary<string, decimal>(),
            _ => new Dictionary<string, decimal>()
        };
    }

    private static string ExtractCreativeId(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("id", out var idElement))
            {
                return idElement.GetString() ?? string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static CreativeChannelScoreResponse Score(string creativeId, IReadOnlyDictionary<string, decimal> metrics)
    {
        return new CreativeChannelScoreResponse
        {
            CreativeId = creativeId,
            Metrics = metrics
        };
    }

    private static string NormalizeChannel(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "ooh" => "billboard",
            "billboards" => "billboard",
            "tv" => "tv",
            "radio" => "radio",
            "digital" => "digital",
            "newspaper" => "newspaper",
            _ => normalized
        };
    }

    private static string NormalizeLanguage(string value)
    {
        var normalized = value.Trim();
        return normalized.Length == 0 ? "English" : char.ToUpperInvariant(normalized[0]) + normalized[1..].ToLowerInvariant();
    }

    private static IReadOnlyList<string> InferChannelsFromPrompt(string prompt)
    {
        var lowered = prompt.ToLowerInvariant();
        var result = new List<string>();
        if (lowered.Contains("radio"))
        {
            result.Add("radio");
        }

        if (lowered.Contains("tv"))
        {
            result.Add("tv");
        }

        if (lowered.Contains("billboard") || lowered.Contains("ooh"))
        {
            result.Add("billboard");
        }

        if (lowered.Contains("newspaper") || lowered.Contains("print"))
        {
            result.Add("newspaper");
        }

        if (lowered.Contains("digital") || lowered.Contains("meta") || lowered.Contains("tiktok"))
        {
            result.Add("digital");
        }

        return result.Count > 0 ? result : new[] { "radio", "billboard", "digital" };
    }

    private static string BuildCtaForObjective(string objective)
    {
        return objective.Trim().ToLowerInvariant() switch
        {
            "foottraffic" => "Visit today",
            "leadgen" => "Get your quote now",
            "sales" => "Buy now",
            _ => "Learn more today"
        };
    }

    private static string HumanizeObjective(string objective)
    {
        return objective.Trim().ToLowerInvariant() switch
        {
            "foottraffic" => "Foot Traffic",
            "leadgen" => "Lead Generation",
            "sales" => "Sales",
            "awareness" => "Awareness",
            _ => objective.Trim()
        };
    }

    private sealed record NormalizedCreativeBrief(
        string Brand,
        string Industry,
        string Location,
        string Objective,
        string Tone,
        string KeyMessage,
        string Cta,
        IReadOnlyList<string> AudienceInsights,
        IReadOnlyList<string> Languages);
}
