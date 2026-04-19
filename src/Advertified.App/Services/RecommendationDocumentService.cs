using System.Text.Json;
using Advertified.App.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class RecommendationDocumentService : IRecommendationDocumentService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IPrivateDocumentStorage _privateDocumentStorage;
    private readonly IPublicAssetStorage _publicAssetStorage;
    private readonly FrontendOptions _frontendOptions;
    private readonly IProposalAccessTokenService _proposalAccessTokenService;
    private readonly ILeadProposalConfidenceGateService _leadProposalConfidenceGateService;

    public RecommendationDocumentService(
        AppDbContext db,
        IWebHostEnvironment environment,
        IPrivateDocumentStorage privateDocumentStorage,
        IPublicAssetStorage publicAssetStorage,
        IOptions<FrontendOptions> frontendOptions,
        IProposalAccessTokenService proposalAccessTokenService,
        ILeadProposalConfidenceGateService leadProposalConfidenceGateService)
    {
        _db = db;
        _environment = environment;
        _privateDocumentStorage = privateDocumentStorage;
        _publicAssetStorage = publicAssetStorage;
        _frontendOptions = frontendOptions.Value;
        _proposalAccessTokenService = proposalAccessTokenService;
        _leadProposalConfidenceGateService = leadProposalConfidenceGateService;
    }

    public async Task<byte[]> GetCampaignPdfBytesAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        await _leadProposalConfidenceGateService.EnsureCampaignReadyAsync(campaignId, cancellationToken);

        var campaign = await LoadCampaignForRecommendationsAsync(campaignId, cancellationToken);
        var opportunityContext = RecommendationOpportunityContextParser.Parse(
            campaign.CampaignBrief?.SpecialRequirements ?? campaign.CampaignBrief?.CreativeNotes);

        var currentRecommendations = RecommendationSelectionPolicy.GetVisibleRecommendationSet(campaign);

        if (currentRecommendations.Length == 0)
        {
            throw new InvalidOperationException("Recommendation not found.");
        }

        var existingSnapshotKey = GetSharedSnapshotKey(currentRecommendations);
        if (!string.IsNullOrWhiteSpace(existingSnapshotKey))
        {
            var existingBytes = await TryGetStoredPdfBytesAsync(existingSnapshotKey, cancellationToken);
            if (existingBytes is not null)
            {
                return existingBytes;
            }
        }

        var model = new RecommendationDocumentModel
        {
            ClientName = campaign.ResolveClientName(),
            BusinessName = campaign.ResolveBusinessName(),
            CampaignName = ResolveCampaignName(campaign),
            CampaignApprovalsUrl = BuildProposalUrl(campaign.Id),
            PackageName = campaign.PackageBand.Name,
            SelectedBudget = ResolveSelectedBudget(campaign),
            BudgetLabel = ResolveBudgetLabel(campaign),
            BudgetDisplayText = ResolveBudgetDisplayText(campaign),
            GeneratedAtUtc = DateTime.UtcNow,
            CampaignObjective = campaign.CampaignBrief?.Objective,
            SpecialRequirements = opportunityContext.CampaignNotes,
            TargetAreas = BuildTargetAreas(campaign.CampaignBrief),
            TargetAudienceSummary = BuildTargetAudienceSummary(campaign.CampaignBrief),
            TargetLanguages = DeserializeList(campaign.CampaignBrief?.TargetLanguagesJson),
            OpportunityContext = opportunityContext.Context,
            Proposals = currentRecommendations.Select((recommendation, index) => MapProposal(campaign.Id, recommendation, opportunityContext.Context, index)).ToArray()
        };

        var logoPath = Billing.InvoicePdfGenerator.ResolveLogoPath(_environment.ContentRootPath, null);
        var pdfBytes = model.OpportunityContext?.IsLeadOutreach == true
            ? LeadOutreachPdfGenerator.Generate(model, logoPath)
            : RecommendationPdfGenerator.Generate(model, logoPath);

        if (ShouldFreezeSnapshot(currentRecommendations))
        {
            var snapshotKey = await PersistPdfAsync(campaign.Id, currentRecommendations[0].RevisionNumber, pdfBytes, cancellationToken);
            foreach (var recommendation in currentRecommendations)
            {
                recommendation.PdfStorageObjectKey = snapshotKey;
                recommendation.PdfGeneratedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        return pdfBytes;
    }

    public async Task<byte[]> GetRecommendationPdfBytesAsync(Guid campaignId, Guid recommendationId, CancellationToken cancellationToken)
    {
        await _leadProposalConfidenceGateService.EnsureCampaignReadyAsync(campaignId, cancellationToken);

        var campaign = await LoadCampaignForRecommendationsAsync(campaignId, cancellationToken);
        var opportunityContext = RecommendationOpportunityContextParser.Parse(
            campaign.CampaignBrief?.SpecialRequirements ?? campaign.CampaignBrief?.CreativeNotes);

        var currentRecommendations = RecommendationSelectionPolicy.GetVisibleRecommendationSet(campaign);
        var recommendation = currentRecommendations.FirstOrDefault(x => x.Id == recommendationId)
            ?? throw new InvalidOperationException("Recommendation not found.");

        if (!string.IsNullOrWhiteSpace(recommendation.PdfStorageObjectKey))
        {
            var existingBytes = await TryGetStoredPdfBytesAsync(recommendation.PdfStorageObjectKey, cancellationToken);
            if (existingBytes is not null)
            {
                return existingBytes;
            }
        }

        var model = new RecommendationDocumentModel
        {
            ClientName = campaign.ResolveClientName(),
            BusinessName = campaign.ResolveBusinessName(),
            CampaignName = ResolveCampaignName(campaign),
            CampaignApprovalsUrl = BuildProposalUrl(campaign.Id),
            PackageName = campaign.PackageBand.Name,
            SelectedBudget = ResolveSelectedBudget(campaign),
            BudgetLabel = ResolveBudgetLabel(campaign),
            BudgetDisplayText = ResolveBudgetDisplayText(campaign),
            GeneratedAtUtc = DateTime.UtcNow,
            CampaignObjective = campaign.CampaignBrief?.Objective,
            SpecialRequirements = opportunityContext.CampaignNotes,
            TargetAreas = BuildTargetAreas(campaign.CampaignBrief),
            TargetAudienceSummary = BuildTargetAudienceSummary(campaign.CampaignBrief),
            TargetLanguages = DeserializeList(campaign.CampaignBrief?.TargetLanguagesJson),
            OpportunityContext = opportunityContext.Context,
            Proposals = new[] { MapProposal(campaign.Id, recommendation, opportunityContext.Context, 0) }
        };

        var logoPath = Billing.InvoicePdfGenerator.ResolveLogoPath(_environment.ContentRootPath, null);
        var pdfBytes = model.OpportunityContext?.IsLeadOutreach == true
            ? LeadOutreachPdfGenerator.Generate(model, logoPath)
            : RecommendationPdfGenerator.Generate(model, logoPath);

        if (ShouldFreezeSnapshot(currentRecommendations))
        {
            var snapshotKey = await PersistProposalPdfAsync(campaign.Id, recommendation.RevisionNumber, recommendation.Id, pdfBytes, cancellationToken);
            recommendation.PdfStorageObjectKey = snapshotKey;
            recommendation.PdfGeneratedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return pdfBytes;
    }

    private RecommendationProposalDocumentModel MapProposal(
        Guid campaignId,
        CampaignRecommendation recommendation,
        RecommendationOpportunityContextModel? opportunityContext,
        int proposalIndex)
    {
        var (label, strategy) = GetProposalDetails(recommendation.RecommendationType, opportunityContext?.ArchetypeName, proposalIndex);
        var lines = recommendation.RecommendationItems
            .OrderBy(item => GetRecommendationItemChannelRank(item.InventoryType))
            .ThenBy(item => item.DisplayName)
            .Select(MapLine)
            .ToList();
        lines.Add(new RecommendationLineDocumentModel
        {
            Channel = "Studio",
            Title = "AI Studio services",
            Rationale = "Creative support and studio services included in campaign workflow.",
            TotalCost = 0m,
            Quantity = 1,
            Restrictions = "Included service line item."
        });

        var proposal = new RecommendationProposalDocumentModel
        {
            Label = label,
            Strategy = strategy,
            AcceptUrl = BuildProposalUrl(campaignId, recommendation.Id),
            Summary = recommendation.Summary ?? string.Empty,
            Rationale = RemoveInternalMarkers(recommendation.Rationale),
            TotalCost = recommendation.TotalCost,
            Items = lines.ToArray()
        };

        return RecommendationClientDocumentShaper.ShapeProposal(proposal);
    }

    private static RecommendationLineDocumentModel MapLine(RecommendationItem item)
    {
        var metadata = NormalizeMetadata(item.MetadataJson);
        return new RecommendationLineDocumentModel
        {
            Channel = item.InventoryType,
            Title = item.DisplayName,
            Rationale = GetMetadataValue(metadata, "rationale") ?? string.Empty,
            TotalCost = item.TotalCost,
            Quantity = item.Quantity,
            Region = GetMetadataValue(metadata, "region"),
            Language = GetMetadataValue(metadata, "language"),
            ShowDaypart = GetMetadataValue(metadata, "showDaypart")
                ?? GetMetadataValue(metadata, "show_daypart")
                ?? GetMetadataValue(metadata, "daypart")
                ?? GetMetadataValue(metadata, "dayType")
                ?? GetMetadataValue(metadata, "day_type"),
            TimeBand = GetMetadataValue(metadata, "timeBand") ?? GetMetadataValue(metadata, "time_band"),
            SlotType = GetMetadataValue(metadata, "slotType") ?? GetMetadataValue(metadata, "slot_type"),
            Duration = GetMetadataValue(metadata, "duration") ?? GetMetadataValue(metadata, "durationSeconds") ?? GetMetadataValue(metadata, "duration_seconds"),
            AppliedDuration = GetMetadataValue(metadata, "appliedDuration") ?? GetMetadataValue(metadata, "appliedDurationLabel") ?? GetMetadataValue(metadata, "applied_duration_label"),
            Flighting = GetMetadataValue(metadata, "flighting"),
            RequestedStartDate = GetMetadataValue(metadata, "requestedStartDate") ?? GetMetadataValue(metadata, "requested_start_date") ?? GetMetadataValue(metadata, "startDate"),
            RequestedEndDate = GetMetadataValue(metadata, "requestedEndDate") ?? GetMetadataValue(metadata, "requested_end_date") ?? GetMetadataValue(metadata, "endDate"),
            ResolvedStartDate = GetMetadataValue(metadata, "resolvedStartDate") ?? GetMetadataValue(metadata, "resolved_start_date") ?? GetMetadataValue(metadata, "startDate"),
            ResolvedEndDate = GetMetadataValue(metadata, "resolvedEndDate") ?? GetMetadataValue(metadata, "resolved_end_date") ?? GetMetadataValue(metadata, "endDate"),
            CommercialExplanation = GetMetadataValue(metadata, "commercialExplanation") ?? GetMetadataValue(metadata, "commercial_explanation"),
            Restrictions = GetMetadataValue(metadata, "restrictions") ?? GetMetadataValue(metadata, "restrictionNotes"),
            Dimensions = GetMetadataValue(metadata, "dimensions"),
            Material = GetMetadataValue(metadata, "material"),
            Illuminated = GetMetadataValue(metadata, "illuminated"),
            TrafficCount = GetMetadataValue(metadata, "trafficCount") ?? GetMetadataValue(metadata, "traffic_count"),
            TargetAudience = GetMetadataValue(metadata, "targetAudience") ?? GetMetadataValue(metadata, "target_audience"),
            AudienceAgeSkew = GetMetadataValue(metadata, "audienceAgeSkew") ?? GetMetadataValue(metadata, "audience_age_skew"),
            AudienceGenderSkew = GetMetadataValue(metadata, "audienceGenderSkew") ?? GetMetadataValue(metadata, "audience_gender_skew"),
            AudienceLsmRange = GetMetadataValue(metadata, "audienceLsmRange") ?? GetMetadataValue(metadata, "audience_lsm_range"),
            ListenershipDaily = GetMetadataValue(metadata, "listenershipDaily") ?? GetMetadataValue(metadata, "listenership_daily"),
            ListenershipWeekly = GetMetadataValue(metadata, "listenershipWeekly") ?? GetMetadataValue(metadata, "listenership_weekly"),
            ListenershipPeriod = GetMetadataValue(metadata, "listenershipPeriod") ?? GetMetadataValue(metadata, "listenership_period"),
            SiteNumber = GetMetadataValue(metadata, "siteNumber") ?? GetMetadataValue(metadata, "site_number"),
            ItemNotes = GetMetadataValue(metadata, "itemNotes"),
            VenueType = GetMetadataValue(metadata, "venueType") ?? GetMetadataValue(metadata, "venue_type"),
            EnvironmentType = GetMetadataValue(metadata, "environmentType") ?? GetMetadataValue(metadata, "environment_type"),
            SelectionReasons = GetMetadataValues(metadata, "selectionReasons"),
            PolicyFlags = GetMetadataValues(metadata, "policyFlags")
        };
    }

    private static Dictionary<string, JsonElement> NormalizeMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!property.NameEquals("Metadata"))
                    {
                        result[property.Name] = property.Value.Clone();
                    }
                }

                if (document.RootElement.TryGetProperty("Metadata", out var nested) && nested.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in nested.EnumerateObject())
                    {
                        if (!result.ContainsKey(property.Name))
                        {
                            result[property.Name] = property.Value.Clone();
                        }
                    }
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? GetMetadataValue(IReadOnlyDictionary<string, JsonElement> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => value.ToString()
        };
    }

    private static IReadOnlyList<string> GetMetadataValues(IReadOnlyDictionary<string, JsonElement> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }

    private static string? BuildTargetAudienceSummary(Data.Entities.CampaignBrief? brief)
    {
        if (brief is null)
        {
            return null;
        }

        var parts = new List<string>();

        if (brief.TargetAgeMin.HasValue || brief.TargetAgeMax.HasValue)
        {
            parts.Add((brief.TargetAgeMin, brief.TargetAgeMax) switch
            {
                ({ } min, { } max) when min == max => $"Age {min}",
                ({ } min, { } max) => $"Ages {min}-{max}",
                ({ } min, null) => $"Age {min}+",
                (null, { } max) => $"Up to age {max}",
                _ => string.Empty
            });
        }

        if (!string.IsNullOrWhiteSpace(brief.TargetGender))
        {
            parts.Add(brief.TargetGender.Trim());
        }

        if (brief.TargetLsmMin.HasValue || brief.TargetLsmMax.HasValue)
        {
            parts.Add((brief.TargetLsmMin, brief.TargetLsmMax) switch
            {
                ({ } min, { } max) when min == max => $"LSM {min}",
                ({ } min, { } max) => $"LSM {min}-{max}",
                ({ } min, null) => $"LSM {min}+",
                (null, { } max) => $"Up to LSM {max}",
                _ => string.Empty
            });
        }

        if (!string.IsNullOrWhiteSpace(brief.TargetAudienceNotes))
        {
            parts.Add(brief.TargetAudienceNotes.Trim());
        }

        var cleaned = parts
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return cleaned.Length == 0 ? null : string.Join(" | ", cleaned);
    }

    private static string RemoveInternalMarkers(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            rationale.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                .Where(line =>
                    !line.StartsWith("Client feedback:", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("Fallback flags:", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("Manual review required:", StringComparison.OrdinalIgnoreCase)))
            .Trim();
    }

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    private static IReadOnlyList<string> BuildTargetAreas(CampaignBrief? brief)
    {
        if (brief is null)
        {
            return Array.Empty<string>();
        }

        var areas = DeserializeList(brief.AreasJson);
        if (areas.Count > 0)
        {
            return areas;
        }

        var cities = DeserializeList(brief.CitiesJson);
        if (cities.Count > 0)
        {
            return cities;
        }

        return DeserializeList(brief.ProvincesJson);
    }

    private static bool ShouldFreezeSnapshot(IEnumerable<CampaignRecommendation> recommendations)
    {
        return recommendations.Any(x =>
            x.SentToClientAt.HasValue ||
            x.ApprovedAt.HasValue ||
            string.Equals(x.Status, RecommendationStatuses.SentToClient, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Status, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetSharedSnapshotKey(IEnumerable<CampaignRecommendation> recommendations)
    {
        var keys = recommendations
            .Select(x => x.PdfStorageObjectKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return keys.Length == 1 ? keys[0] : null;
    }

    private async Task<string> PersistPdfAsync(Guid campaignId, int revisionNumber, byte[] pdfBytes, CancellationToken cancellationToken)
    {
        var objectKey = $"recommendations/campaign-{campaignId:D}/revision-{revisionNumber:D3}.pdf";
        return await _privateDocumentStorage.SaveAsync(objectKey, pdfBytes, "application/pdf", cancellationToken);
    }

    private async Task<string> PersistProposalPdfAsync(Guid campaignId, int revisionNumber, Guid recommendationId, byte[] pdfBytes, CancellationToken cancellationToken)
    {
        var objectKey = $"recommendations/campaign-{campaignId:D}/revision-{revisionNumber:D3}/proposal-{recommendationId:D}.pdf";
        return await _privateDocumentStorage.SaveAsync(objectKey, pdfBytes, "application/pdf", cancellationToken);
    }

    private async Task<byte[]?> TryGetStoredPdfBytesAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            return await _privateDocumentStorage.GetBytesAsync(objectKey, cancellationToken);
        }
        catch (InvalidOperationException)
        {
        }

        try
        {
            return await _publicAssetStorage.GetBytesAsync(objectKey, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<Campaign> LoadCampaignForRecommendationsAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _db.Campaigns
            .AsSplitQuery()
            .Include(x => x.User!)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.ProspectLead)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");
    }

    private static string ResolveCampaignName(Campaign campaign)
    {
        return string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim();
    }

    private static decimal ResolveSelectedBudget(Campaign campaign)
    {
        return PricingPolicy.ResolvePlanningBudget(
            campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
            campaign.PackageOrder.AiStudioReserveAmount);
    }

    private static string ResolveBudgetLabel(Campaign campaign)
    {
        return ShouldDisplayPackageRange(campaign)
            ? "Package range"
            : "Selected budget";
    }

    private static string ResolveBudgetDisplayText(Campaign campaign)
    {
        if (ShouldDisplayPackageRange(campaign))
        {
            return $"{FormatCurrency(campaign.PackageBand.MinBudget)} to {FormatCurrency(campaign.PackageBand.MaxBudget)}";
        }

        return FormatCurrency(ResolveSelectedBudget(campaign));
    }

    private static bool ShouldDisplayPackageRange(Campaign campaign)
    {
        return string.Equals(campaign.Status, CampaignStatuses.AwaitingPurchase, StringComparison.OrdinalIgnoreCase)
            || !CampaignOperationsPolicy.IsOrderOperationallyActive(campaign.PackageOrder);
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount:N2}";
    }

    private static (string Label, string Strategy) GetProposalDetails(string? recommendationType, string? archetypeName, int proposalIndex)
    {
        var archetypePlaybook = ResolveArchetypeProposalPlaybook(archetypeName, proposalIndex);
        if (archetypePlaybook is not null)
        {
            return archetypePlaybook.Value;
        }

        var variantKey = recommendationType?
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()?
            .ToLowerInvariant();

        return variantKey switch
        {
            "balanced" => ("Proposal A", "Balanced mix"),
            "ooh_focus" => ("Proposal B", "Billboards and Digital Screens-led reach"),
            "radio_focus" => ("Proposal C", "Radio-led frequency"),
            "digital_focus" => ("Proposal C", "Digital-led amplification"),
            "tv_focus" => ("Proposal C", "TV-led reach"),
            _ => ($"Proposal {GetProposalLetter(proposalIndex)}", "Recommendation option")
        };
    }

    private static (string Label, string Strategy)? ResolveArchetypeProposalPlaybook(string? archetypeName, int proposalIndex)
    {
        if (string.IsNullOrWhiteSpace(archetypeName))
        {
            return null;
        }

        var normalized = archetypeName.Trim().ToLowerInvariant();
        return normalized switch
        {
            "active scaler" => proposalIndex switch
            {
                0 => ("Proposal A — Channel Extension (Test & validate)", "Add one missing high-impact channel while maintaining current digital activity. Built for low-risk validation."),
                1 => ("Proposal B — Multi-Channel Expansion (Best balance)", "Capture more demand across multiple touchpoints with a balanced radio, Billboards and Digital Screens, and digital reinforcement mix."),
                _ => ("Proposal C — Market Domination (Max growth)", "Maximise visibility and push category dominance locally through stronger radio, Billboards and Digital Screens, and digital amplification.")
            },
            "promo-dependent retailer" => proposalIndex switch
            {
                0 => ("Proposal A — Awareness Starter (Test & validate)", "Introduce a light brand layer with small radio support and minimal digital continuity."),
                1 => ("Proposal B — Promo + Brand Hybrid (Best balance)", "Turn promotions into more consistent foot traffic using radio consistency, Billboards and Digital Screens reinforcement, and digital amplification."),
                _ => ("Proposal C — Always-On Presence (Max growth)", "Build continuous visibility with stronger radio, Billboards and Digital Screens presence, and ongoing digital support to reduce promo dependency.")
            },
            "invisible local business" => proposalIndex switch
            {
                0 => ("Proposal A — Discovery Fix (Test & validate)", "Fix local discovery with search presence, maps optimization, and light paid support."),
                1 => ("Proposal B — Local Visibility Boost (Best balance)", "Combine search and maps with radio or entry-level Billboards and Digital Screens to improve both discoverability and local awareness."),
                _ => ("Proposal C — Full Local Dominance (Max growth)", "Own local visibility across search, Billboards and Digital Screens, and radio to become the most visible option in-area.")
            },
            "digital-only player" => proposalIndex switch
            {
                0 => ("Proposal A — Offline Test (Test & validate)", "Run a first offline step with small radio or 1-2 Billboards and Digital Screens placements while maintaining digital performance."),
                1 => ("Proposal B — Omnichannel Expansion (Best balance)", "Break through digital saturation by combining current digital strength with balanced radio and Billboards and Digital Screens."),
                _ => ("Proposal C — Scale Reach Fast (Max growth)", "Expand audience reach aggressively through heavier Billboards and Digital Screens, stronger radio coverage, and digital amplification.")
            },
            "passive / untapped business" => proposalIndex switch
            {
                0 => ("Proposal A — Starter Campaign (Test & validate)", "Launch a simple low-risk starter campaign with clear execution steps and BNPL support."),
                1 => ("Proposal B — Growth Foundation (Best balance)", "Build a more consistent customer flow with a practical step-up in channel coverage."),
                _ => ("Proposal C — Acceleration (Max growth)", "Fast-track growth with a broader multi-channel starter mix and stronger visibility pressure.")
            },
            _ => null
        };
    }

    private static string GetProposalLetter(int index)
    {
        return index >= 0 && index < 26
            ? ((char)('A' + index)).ToString()
            : (index + 1).ToString();
    }

    private static int GetRecommendationItemChannelRank(string? channel)
    {
        var normalized = NormalizeRecommendationChannel(channel);
        return normalized switch
        {
            "ooh" => 0,
            "radio" => 1,
            "tv" => 2,
            "digital" => 3,
            _ => 9
        };
    }

    private static string NormalizeRecommendationChannel(string? channel)
    {
        var normalized = (channel ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("ooh", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("billboard", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("out of home", StringComparison.OrdinalIgnoreCase))
        {
            return "ooh";
        }

        if (normalized.Contains("radio", StringComparison.OrdinalIgnoreCase))
        {
            return "radio";
        }

        if (normalized.Contains("tv", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("television", StringComparison.OrdinalIgnoreCase))
        {
            return "tv";
        }

        if (normalized.Contains("digital", StringComparison.OrdinalIgnoreCase))
        {
            return "digital";
        }

        return normalized;
    }

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private string BuildProposalUrl(Guid campaignId, Guid? recommendationId = null)
    {
        var query = new List<string>
        {
            $"token={Uri.EscapeDataString(_proposalAccessTokenService.CreateToken(campaignId))}"
        };

        if (recommendationId.HasValue)
        {
            query.Add($"recommendationId={Uri.EscapeDataString(recommendationId.Value.ToString("D"))}");
        }

        var queryString = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;
        return BuildFrontendUrl($"/proposal/{campaignId:D}{queryString}");
    }
}

internal static class RecommendationOpportunityContextParser
{
    public static RecommendationOpportunityContextParseResult Parse(string? rawNotes)
    {
        if (string.IsNullOrWhiteSpace(rawNotes))
        {
            return new RecommendationOpportunityContextParseResult(null, null);
        }

        var sections = rawNotes
            .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static section => !string.IsNullOrWhiteSpace(section))
            .ToList();

        var detectedGaps = new List<string>();
        string? archetypeName = null;
        string? industryProfileName = null;
        string? industryMessagingAngle = null;
        var industryGuardrails = new List<string>();
        string? industryRecommendedCta = null;
        string? whoWeAre = null;
        var researchBasis = new List<string>();
        string? lastResearchedAtUtc = null;
        string? socialQualityNote = null;
        string? leadInsightSummary = null;
        string? expectedOutcome = null;
        string? whyActNow = null;
        string? flexibleRollout = null;
        string? nextStep = null;
        var isLeadOutreach = false;
        var remainingSections = new List<string>();

        foreach (var section in sections)
        {
            if (TryParseDetectedGaps(section, detectedGaps))
            {
                isLeadOutreach = true;
                continue;
            }

            if (TryParsePrefixedSection(section, "Archetype:", out var archetype))
            {
                archetypeName = archetype;
                isLeadOutreach = true;
                continue;
            }

            if (TryParsePrefixedSection(section, "Who we are:", out var intro))
            {
                whoWeAre = intro;
                continue;
            }

            if (TryParsePrefixedSection(section, "Industry profile:", out var industryProfile))
            {
                industryProfileName = industryProfile;
                continue;
            }

            if (TryParsePrefixedSection(section, "Industry messaging angle:", out var angle))
            {
                industryMessagingAngle = angle;
                continue;
            }

            if (TryParseBulletedSection(section, "Industry guardrails:", industryGuardrails))
            {
                continue;
            }

            if (TryParsePrefixedSection(section, "Industry recommended CTA:", out var industryCta))
            {
                industryRecommendedCta = industryCta;
                continue;
            }

            if (TryParseBulletedSection(section, "Research basis:", researchBasis))
            {
                continue;
            }

            if (TryParsePrefixedSection(section, "Last researched:", out var lastResearched))
            {
                lastResearchedAtUtc = lastResearched;
                continue;
            }

            if (TryParsePrefixedSection(section, "Social quality note:", out var socialNote))
            {
                socialQualityNote = socialNote;
                continue;
            }

            if (TryParsePrefixedSection(section, "Lead intelligence summary:", out var summary))
            {
                leadInsightSummary = summary;
                continue;
            }

            if (section.StartsWith("Expected impact:", StringComparison.OrdinalIgnoreCase))
            {
                expectedOutcome = section.Trim();
                continue;
            }

            if (TryParsePrefixedSection(section, "Why act now:", out var urgency))
            {
                whyActNow = urgency;
                continue;
            }

            if (TryParsePrefixedSection(section, "Flexible rollout:", out var rollout))
            {
                flexibleRollout = rollout;
                continue;
            }

            if (TryParsePrefixedSection(section, "Next step:", out var step))
            {
                nextStep = step;
                continue;
            }

            if (TryParsePrefixedSection(section, "Outreach email draft:", out _))
            {
                // Outreach email body is CRM/operator content and should never appear inside proposal PDFs.
                continue;
            }

            if (TryParsePrefixedSection(section, "Lead source id:", out _))
            {
                // Internal provenance marker used for server-side confidence gate checks.
                continue;
            }

            remainingSections.Add(section.Trim());
        }

        var hasOpportunityContext = detectedGaps.Count > 0
            || !string.IsNullOrWhiteSpace(archetypeName)
            || !string.IsNullOrWhiteSpace(industryProfileName)
            || !string.IsNullOrWhiteSpace(industryMessagingAngle)
            || industryGuardrails.Count > 0
            || !string.IsNullOrWhiteSpace(industryRecommendedCta)
            || !string.IsNullOrWhiteSpace(whoWeAre)
            || researchBasis.Count > 0
            || !string.IsNullOrWhiteSpace(lastResearchedAtUtc)
            || !string.IsNullOrWhiteSpace(socialQualityNote)
            || !string.IsNullOrWhiteSpace(leadInsightSummary)
            || !string.IsNullOrWhiteSpace(expectedOutcome)
            || !string.IsNullOrWhiteSpace(whyActNow)
            || !string.IsNullOrWhiteSpace(flexibleRollout)
            || !string.IsNullOrWhiteSpace(nextStep);

        var campaignNotes = remainingSections.Count > 0
            ? string.Join(Environment.NewLine + Environment.NewLine, remainingSections)
            : null;

        return new RecommendationOpportunityContextParseResult(
            hasOpportunityContext
                ? new RecommendationOpportunityContextModel
                {
                    ArchetypeName = archetypeName,
                    IndustryProfileName = industryProfileName,
                    IndustryMessagingAngle = industryMessagingAngle,
                    IndustryGuardrails = industryGuardrails,
                    IndustryRecommendedCta = industryRecommendedCta,
                    WhoWeAre = whoWeAre,
                    ResearchBasis = researchBasis,
                    LastResearchedAtUtc = lastResearchedAtUtc,
                    SocialQualityNote = socialQualityNote,
                    DetectedGaps = detectedGaps,
                    LeadInsightSummary = leadInsightSummary,
                    ExpectedOutcome = expectedOutcome,
                    WhyActNow = whyActNow,
                    FlexibleRollout = flexibleRollout,
                    NextStep = nextStep,
                    IsLeadOutreach = isLeadOutreach
                }
                : null,
            campaignNotes);
    }

    private static bool TryParseDetectedGaps(string section, List<string> detectedGaps)
    {
        return TryParseBulletedSection(section, "Why you are receiving this:", detectedGaps);
    }

    private static bool TryParseBulletedSection(string section, string prefix, List<string> lines)
    {
        if (!TryParsePrefixedSection(section, prefix, out var body))
        {
            return false;
        }

        foreach (var line in body
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith("-", StringComparison.Ordinal)))
        {
            var normalized = line[1..].Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                lines.Add(normalized);
            }
        }

        return true;
    }

    private static bool TryParsePrefixedSection(string section, string prefix, out string value)
    {
        if (!section.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = string.Empty;
            return false;
        }

        value = section[prefix.Length..].Trim();
        return true;
    }
}

internal sealed record RecommendationOpportunityContextParseResult(
    RecommendationOpportunityContextModel? Context,
    string? CampaignNotes);
