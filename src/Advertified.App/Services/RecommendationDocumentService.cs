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
    private readonly IPublicAssetStorage _assetStorage;
    private readonly FrontendOptions _frontendOptions;

    public RecommendationDocumentService(
        AppDbContext db,
        IWebHostEnvironment environment,
        IPublicAssetStorage assetStorage,
        IOptions<FrontendOptions> frontendOptions)
    {
        _db = db;
        _environment = environment;
        _assetStorage = assetStorage;
        _frontendOptions = frontendOptions.Value;
    }

    public async Task<byte[]> GetCampaignPdfBytesAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await LoadCampaignForRecommendationsAsync(campaignId, cancellationToken);

        var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);

        if (currentRecommendations.Length == 0)
        {
            throw new InvalidOperationException("Recommendation not found.");
        }

        var existingSnapshotKey = GetSharedSnapshotKey(currentRecommendations);
        if (!string.IsNullOrWhiteSpace(existingSnapshotKey))
        {
            try
            {
                return await _assetStorage.GetBytesAsync(existingSnapshotKey, cancellationToken);
            }
            catch (InvalidOperationException)
            {
            }
        }

        var model = new RecommendationDocumentModel
        {
            ClientName = campaign.User.FullName,
            BusinessName = campaign.User.BusinessProfile?.BusinessName,
            CampaignName = ResolveCampaignName(campaign),
            CampaignApprovalsUrl = BuildProposalUrl(campaign.Id),
            PackageName = campaign.PackageBand.Name,
            SelectedBudget = ResolveSelectedBudget(campaign),
            GeneratedAtUtc = DateTime.UtcNow,
            CampaignObjective = campaign.CampaignBrief?.Objective,
            SpecialRequirements = campaign.CampaignBrief?.SpecialRequirements ?? campaign.CampaignBrief?.CreativeNotes,
            TargetAreas = BuildTargetAreas(campaign.CampaignBrief),
            TargetLanguages = DeserializeList(campaign.CampaignBrief?.TargetLanguagesJson),
            Proposals = currentRecommendations.Select((recommendation, index) => MapProposal(campaign.Id, recommendation, index)).ToArray()
        };

        var logoPath = Billing.InvoicePdfGenerator.ResolveLogoPath(_environment.ContentRootPath, null);
        var pdfBytes = RecommendationPdfGenerator.Generate(model, logoPath);

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
        var campaign = await LoadCampaignForRecommendationsAsync(campaignId, cancellationToken);

        var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
        var recommendation = currentRecommendations.FirstOrDefault(x => x.Id == recommendationId)
            ?? throw new InvalidOperationException("Recommendation not found.");

        if (!string.IsNullOrWhiteSpace(recommendation.PdfStorageObjectKey))
        {
            try
            {
                return await _assetStorage.GetBytesAsync(recommendation.PdfStorageObjectKey, cancellationToken);
            }
            catch (InvalidOperationException)
            {
            }
        }

        var model = new RecommendationDocumentModel
        {
            ClientName = campaign.User.FullName,
            BusinessName = campaign.User.BusinessProfile?.BusinessName,
            CampaignName = ResolveCampaignName(campaign),
            CampaignApprovalsUrl = BuildProposalUrl(campaign.Id),
            PackageName = campaign.PackageBand.Name,
            SelectedBudget = ResolveSelectedBudget(campaign),
            GeneratedAtUtc = DateTime.UtcNow,
            CampaignObjective = campaign.CampaignBrief?.Objective,
            SpecialRequirements = campaign.CampaignBrief?.SpecialRequirements ?? campaign.CampaignBrief?.CreativeNotes,
            TargetAreas = BuildTargetAreas(campaign.CampaignBrief),
            TargetLanguages = DeserializeList(campaign.CampaignBrief?.TargetLanguagesJson),
            Proposals = new[] { MapProposal(campaign.Id, recommendation, 0) }
        };

        var logoPath = Billing.InvoicePdfGenerator.ResolveLogoPath(_environment.ContentRootPath, null);
        var pdfBytes = RecommendationPdfGenerator.Generate(model, logoPath);

        if (ShouldFreezeSnapshot(currentRecommendations))
        {
            var snapshotKey = await PersistProposalPdfAsync(campaign.Id, recommendation.RevisionNumber, recommendation.Id, pdfBytes, cancellationToken);
            recommendation.PdfStorageObjectKey = snapshotKey;
            recommendation.PdfGeneratedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return pdfBytes;
    }

    private RecommendationProposalDocumentModel MapProposal(Guid campaignId, CampaignRecommendation recommendation, int proposalIndex)
    {
        var (label, strategy) = GetProposalDetails(recommendation.RecommendationType, proposalIndex);
        var lines = recommendation.RecommendationItems.Select(MapLine).ToList();
        lines.Add(new RecommendationLineDocumentModel
        {
            Channel = "Studio",
            Title = "AI Studio services",
            Rationale = "Creative support and studio services included in campaign workflow.",
            Quantity = 1,
            Restrictions = "Included service line item."
        });

        return new RecommendationProposalDocumentModel
        {
            Label = label,
            Strategy = strategy,
            AcceptUrl = BuildProposalUrl(campaignId, recommendation.Id),
            Summary = recommendation.Summary ?? string.Empty,
            Rationale = RemoveInternalMarkers(recommendation.Rationale),
            TotalCost = recommendation.TotalCost,
            Items = lines.ToArray()
        };
    }

    private static RecommendationLineDocumentModel MapLine(RecommendationItem item)
    {
        var metadata = NormalizeMetadata(item.MetadataJson);
        return new RecommendationLineDocumentModel
        {
            Channel = item.InventoryType,
            Title = item.DisplayName,
            Rationale = GetMetadataValue(metadata, "rationale") ?? string.Empty,
            Quantity = item.Quantity,
            Region = GetMetadataValue(metadata, "region"),
            Language = GetMetadataValue(metadata, "language"),
            TimeBand = GetMetadataValue(metadata, "timeBand") ?? GetMetadataValue(metadata, "time_band"),
            SlotType = GetMetadataValue(metadata, "slotType") ?? GetMetadataValue(metadata, "slot_type"),
            Duration = GetMetadataValue(metadata, "duration") ?? GetMetadataValue(metadata, "durationSeconds") ?? GetMetadataValue(metadata, "duration_seconds"),
            Flighting = GetMetadataValue(metadata, "flighting"),
            Restrictions = GetMetadataValue(metadata, "restrictions") ?? GetMetadataValue(metadata, "restrictionNotes"),
            Dimensions = GetMetadataValue(metadata, "dimensions"),
            Material = GetMetadataValue(metadata, "material"),
            Illuminated = GetMetadataValue(metadata, "illuminated"),
            TrafficCount = GetMetadataValue(metadata, "trafficCount") ?? GetMetadataValue(metadata, "traffic_count"),
            SiteNumber = GetMetadataValue(metadata, "siteNumber") ?? GetMetadataValue(metadata, "site_number"),
            ItemNotes = GetMetadataValue(metadata, "itemNotes"),
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
        return await _assetStorage.SaveAsync(objectKey, pdfBytes, "application/pdf", cancellationToken);
    }

    private async Task<string> PersistProposalPdfAsync(Guid campaignId, int revisionNumber, Guid recommendationId, byte[] pdfBytes, CancellationToken cancellationToken)
    {
        var objectKey = $"recommendations/campaign-{campaignId:D}/revision-{revisionNumber:D3}/proposal-{recommendationId:D}.pdf";
        return await _assetStorage.SaveAsync(objectKey, pdfBytes, "application/pdf", cancellationToken);
    }

    private async Task<Campaign> LoadCampaignForRecommendationsAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _db.Campaigns
            .AsSplitQuery()
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
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

    private static (string Label, string Strategy) GetProposalDetails(string? recommendationType, int proposalIndex)
    {
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

    private static string GetProposalLetter(int index)
    {
        return index >= 0 && index < 26
            ? ((char)('A' + index)).ToString()
            : (index + 1).ToString();
    }

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private string BuildProposalUrl(Guid campaignId, Guid? recommendationId = null)
    {
        var query = new List<string>();

        if (recommendationId.HasValue)
        {
            query.Add($"recommendationId={Uri.EscapeDataString(recommendationId.Value.ToString("D"))}");
        }

        var queryString = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;
        return BuildFrontendUrl($"/proposal/{campaignId:D}{queryString}");
    }
}
