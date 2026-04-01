using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class AiCostEstimator : IAiCostEstimator
{
    private readonly AiPlatformOptions _options;

    public AiCostEstimator(IOptions<AiPlatformOptions> options)
    {
        _options = options.Value;
    }

    public decimal CalculateMaxAiCost(decimal campaignBudget)
    {
        if (campaignBudget <= 0m)
        {
            return 0m;
        }

        var percentCap = campaignBudget * Math.Max(0m, _options.MaxAiCostPercentOfCampaignBudget);
        var hardCap = Math.Max(0m, _options.MaxAiCostHardCapZar);
        return Math.Min(percentCap, hardCap);
    }

    public int ResolveVariantCount(decimal campaignBudget)
    {
        if (campaignBudget < 10000m)
        {
            return 1;
        }

        if (campaignBudget < 50000m)
        {
            return 2;
        }

        return 3;
    }

    public bool AllowVideoGeneration(decimal campaignBudget)
    {
        return campaignBudget >= Math.Max(0m, _options.VideoMinCampaignBudgetZar);
    }

    public decimal EstimateTextGenerationCost(int variantCount)
    {
        return Math.Max(0, variantCount) * Math.Max(0m, _options.TextGenerationCostZar);
    }

    public decimal EstimateQaCost(int variantCount)
    {
        return Math.Max(0, variantCount) * Math.Max(0m, _options.QaScoringCostZar);
    }

    public decimal EstimateAssetCost(string assetKind, int units = 1)
    {
        var count = Math.Max(0, units);
        var unitCost = assetKind.Trim().ToLowerInvariant() switch
        {
            "voice" => _options.VoiceGenerationCostZar,
            "video" => _options.VideoGenerationCostZar,
            _ => _options.ImageGenerationCostZar
        };

        return count * Math.Max(0m, unitCost);
    }
}

public sealed class AiCostControlService : IAiCostControlService
{
    private static readonly string[] CommittedStatuses = { "reserved", "completed" };

    private readonly AppDbContext _db;
    private readonly IAiCostEstimator _estimator;
    private readonly AiPlatformOptions _options;

    public AiCostControlService(AppDbContext db, IAiCostEstimator estimator, IOptions<AiPlatformOptions> options)
    {
        _db = db;
        _estimator = estimator;
        _options = options.Value;
    }

    public async Task<AiCostGuardDecision> GuardAsync(AiCostGuardRequest request, CancellationToken cancellationToken)
    {
        var campaignBudget = request.CampaignBudgetZar ?? await ResolveCampaignBudgetAsync(request.CampaignId, cancellationToken);
        var maxAllowed = await ResolveMaxAllowedCostAsync(request.CampaignId, campaignBudget, cancellationToken);
        var committed = await GetCommittedCostAsync(request.CampaignId, cancellationToken);
        var projected = committed + Math.Max(0m, request.EstimatedCostZar);

        if (projected > maxAllowed)
        {
            // We keep rejected attempts for auditability and abuse tracking.
            _db.AiUsageLogs.Add(new AiUsageLog
            {
                Id = Guid.NewGuid(),
                CampaignId = request.CampaignId,
                CreativeId = request.CreativeId,
                JobId = request.JobId,
                Operation = request.Operation,
                Provider = request.Provider,
                EstimatedCostZar = Math.Max(0m, request.EstimatedCostZar),
                ActualCostZar = null,
                Status = "rejected",
                Details = request.Details,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);

            return new AiCostGuardDecision(
                Allowed: false,
                UsageLogId: null,
                CampaignBudgetZar: campaignBudget,
                MaxAllowedCostZar: maxAllowed,
                CurrentCommittedCostZar: committed,
                ProjectedCommittedCostZar: projected,
                Message: "AI cost cap reached for this campaign.");
        }

        var usageLogId = Guid.NewGuid();
        _db.AiUsageLogs.Add(new AiUsageLog
        {
            Id = usageLogId,
            CampaignId = request.CampaignId,
            CreativeId = request.CreativeId,
            JobId = request.JobId,
            Operation = request.Operation,
            Provider = request.Provider,
            EstimatedCostZar = Math.Max(0m, request.EstimatedCostZar),
            ActualCostZar = null,
            Status = "reserved",
            Details = request.Details,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new AiCostGuardDecision(
            Allowed: true,
            UsageLogId: usageLogId,
            CampaignBudgetZar: campaignBudget,
            MaxAllowedCostZar: maxAllowed,
            CurrentCommittedCostZar: committed,
            ProjectedCommittedCostZar: projected,
            Message: "Cost reservation accepted.");
    }

    public async Task CompleteAsync(Guid usageLogId, decimal? actualCostZar, string? details, CancellationToken cancellationToken)
    {
        var row = await _db.AiUsageLogs.FirstOrDefaultAsync(x => x.Id == usageLogId, cancellationToken);
        if (row is null)
        {
            return;
        }

        row.Status = "completed";
        row.ActualCostZar = actualCostZar;
        row.Details = details ?? row.Details;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid usageLogId, string? details, CancellationToken cancellationToken)
    {
        var row = await _db.AiUsageLogs.FirstOrDefaultAsync(x => x.Id == usageLogId, cancellationToken);
        if (row is null)
        {
            return;
        }

        row.Status = "failed";
        row.Details = details ?? row.Details;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AiCampaignCostSummary> GetSummaryAsync(Guid campaignId, decimal? campaignBudgetZar, CancellationToken cancellationToken)
    {
        var budget = campaignBudgetZar ?? await ResolveCampaignBudgetAsync(campaignId, cancellationToken);
        var maxAllowed = await ResolveMaxAllowedCostAsync(campaignId, budget, cancellationToken);
        var committed = await GetCommittedCostAsync(campaignId, cancellationToken);
        var remaining = Math.Max(0m, maxAllowed - committed);
        var utilization = maxAllowed <= 0m
            ? 0m
            : Math.Round((committed / maxAllowed) * 100m, 2, MidpointRounding.AwayFromZero);

        return new AiCampaignCostSummary(
            campaignId,
            budget,
            maxAllowed,
            committed,
            remaining,
            utilization);
    }

    private async Task<decimal> ResolveCampaignBudgetAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found for cost calculation.");

        return campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount;
    }

    private async Task<decimal> GetCommittedCostAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _db.AiUsageLogs
            .AsNoTracking()
            .Where(x => x.CampaignId == campaignId && CommittedStatuses.Contains(x.Status))
            .SumAsync(x => x.ActualCostZar ?? x.EstimatedCostZar, cancellationToken);
    }

    private async Task<decimal> ResolveMaxAllowedCostAsync(Guid campaignId, decimal campaignBudget, CancellationToken cancellationToken)
    {
        var reservePercent = await ResolveAiReservePercentAsync(campaignId, cancellationToken);
        var safeReserve = Math.Max(0m, reservePercent) * Math.Max(0m, _options.SpendSafetyFactorOfAiReserve);
        var hardCap = Math.Max(0m, _options.MaxAiCostHardCapZar);

        // Fallback to legacy direct-percent cap only if reserve-based value is unavailable.
        if (safeReserve <= 0m)
        {
            return _estimator.CalculateMaxAiCost(campaignBudget);
        }

        return Math.Min(campaignBudget * safeReserve, hardCap);
    }

    private async Task<decimal> ResolveAiReservePercentAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaignReserve = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.PackageOrder)
            .Where(x => x.Id == campaignId)
            .Select(x => (decimal?)x.PackageOrder.AiStudioReservePercent)
            .FirstOrDefaultAsync(cancellationToken);
        if (campaignReserve.HasValue && campaignReserve.Value > 0m)
        {
            return campaignReserve.Value;
        }

        var pricingDefault = await _db.PricingSettings
            .AsNoTracking()
            .Where(x => x.PricingKey == "default")
            .Select(x => (decimal?)x.AiStudioReservePercent)
            .FirstOrDefaultAsync(cancellationToken);
        if (pricingDefault.HasValue && pricingDefault.Value > 0m)
        {
            return pricingDefault.Value;
        }

        return 0.10m;
    }
}
