using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class AdminLeadIntelligenceSettingsService : IAdminLeadIntelligenceSettingsService
{
    private static readonly IReadOnlyDictionary<string, string> SettingDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["scoring_base_score"] = "Base lead intent score applied before activity and opportunity adjustments.",
        ["scoring_activity_promo_active"] = "Score bonus when promotional activity is detected.",
        ["scoring_activity_meta_strong"] = "Score bonus when social/meta activity is strong.",
        ["scoring_activity_website_active"] = "Score bonus when the website shows recent activity.",
        ["scoring_activity_multi_channel_presence"] = "Score bonus when multiple active channels are detected.",
        ["scoring_opportunity_digital_strong_but_search_weak"] = "Opportunity score bonus when digital is strong but search capture is weak.",
        ["scoring_opportunity_digital_strong_but_ooh_weak"] = "Opportunity score bonus when digital is strong but OOH is weak.",
        ["scoring_opportunity_promo_heavy_but_brand_presence_weak"] = "Opportunity score bonus when promotions are visible but broad-reach presence is weak.",
        ["scoring_opportunity_single_channel_dependency"] = "Opportunity score bonus when only one active channel is detected.",
        ["scoring_threshold_strong_channel_min"] = "Minimum channel score considered strong.",
        ["scoring_threshold_weak_channel_max"] = "Maximum channel score still treated as weak.",
        ["scoring_threshold_active_channel_min"] = "Minimum channel score considered active.",
        ["scoring_intent_low_max"] = "Maximum score still classified as low intent.",
        ["scoring_intent_medium_max"] = "Maximum score still classified as medium intent.",
        ["automation_enabled"] = "Enable scheduled lead intelligence refresh processing.",
        ["automation_refresh_interval_minutes"] = "Minutes between scheduled lead intelligence refresh passes.",
        ["automation_batch_size"] = "Maximum number of leads processed per scheduled batch.",
        ["automation_run_on_startup"] = "Run the lead intelligence jobs once when the API starts.",
        ["automation_enable_paid_media_evidence_sync"] = "Enable scheduled paid media evidence sync jobs.",
        ["automation_paid_media_sync_interval_minutes"] = "Minutes between paid media evidence sync runs."
    };

    private readonly AppDbContext _db;
    private readonly LeadScoringSettingsSnapshotProvider _scoringSnapshotProvider;
    private readonly LeadIntelligenceAutomationSnapshotProvider _automationSnapshotProvider;

    public AdminLeadIntelligenceSettingsService(
        AppDbContext db,
        LeadScoringSettingsSnapshotProvider scoringSnapshotProvider,
        LeadIntelligenceAutomationSnapshotProvider automationSnapshotProvider)
    {
        _db = db;
        _scoringSnapshotProvider = scoringSnapshotProvider;
        _automationSnapshotProvider = automationSnapshotProvider;
    }

    public Task<AdminLeadIntelligenceSettingsResponse> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var scoring = _scoringSnapshotProvider.GetCurrent();
        var automation = _automationSnapshotProvider.GetCurrent();

        return Task.FromResult(new AdminLeadIntelligenceSettingsResponse
        {
            Scoring = new AdminLeadScoringSettingsResponse
            {
                BaseScore = scoring.BaseScore,
                ActivityWeights = new AdminLeadActivityScoringSettingsResponse
                {
                    PromoActive = scoring.ActivityWeights.PromoActive,
                    MetaStrong = scoring.ActivityWeights.MetaStrong,
                    WebsiteActive = scoring.ActivityWeights.WebsiteActive,
                    MultiChannelPresence = scoring.ActivityWeights.MultiChannelPresence
                },
                OpportunityWeights = new AdminLeadOpportunityScoringSettingsResponse
                {
                    DigitalStrongButSearchWeak = scoring.OpportunityWeights.DigitalStrongButSearchWeak,
                    DigitalStrongButOohWeak = scoring.OpportunityWeights.DigitalStrongButOohWeak,
                    PromoHeavyButBrandPresenceWeak = scoring.OpportunityWeights.PromoHeavyButBrandPresenceWeak,
                    SingleChannelDependency = scoring.OpportunityWeights.SingleChannelDependency
                },
                SignalThresholds = new AdminLeadScoringSignalThresholdsResponse
                {
                    StrongChannelMin = scoring.SignalThresholds.StrongChannelMin,
                    WeakChannelMax = scoring.SignalThresholds.WeakChannelMax,
                    ActiveChannelMin = scoring.SignalThresholds.ActiveChannelMin
                },
                Thresholds = new AdminLeadIntentThresholdsResponse
                {
                    LowMax = scoring.Thresholds.LowMax,
                    MediumMax = scoring.Thresholds.MediumMax
                }
            },
            Automation = new AdminLeadIntelligenceAutomationSettingsResponse
            {
                Enabled = automation.Enabled,
                RefreshIntervalMinutes = automation.RefreshIntervalMinutes,
                BatchSize = automation.BatchSize,
                RunOnStartup = automation.RunOnStartup,
                EnablePaidMediaEvidenceSync = automation.EnablePaidMediaEvidenceSync,
                PaidMediaSyncIntervalMinutes = automation.PaidMediaSyncIntervalMinutes
            }
        });
    }

    public async Task UpdateScoringAsync(UpdateAdminLeadScoringSettingsRequest request, CancellationToken cancellationToken)
    {
        ValidateScoring(request);
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["scoring_base_score"] = request.BaseScore.ToString(),
            ["scoring_activity_promo_active"] = request.ActivityWeights.PromoActive.ToString(),
            ["scoring_activity_meta_strong"] = request.ActivityWeights.MetaStrong.ToString(),
            ["scoring_activity_website_active"] = request.ActivityWeights.WebsiteActive.ToString(),
            ["scoring_activity_multi_channel_presence"] = request.ActivityWeights.MultiChannelPresence.ToString(),
            ["scoring_opportunity_digital_strong_but_search_weak"] = request.OpportunityWeights.DigitalStrongButSearchWeak.ToString(),
            ["scoring_opportunity_digital_strong_but_ooh_weak"] = request.OpportunityWeights.DigitalStrongButOohWeak.ToString(),
            ["scoring_opportunity_promo_heavy_but_brand_presence_weak"] = request.OpportunityWeights.PromoHeavyButBrandPresenceWeak.ToString(),
            ["scoring_opportunity_single_channel_dependency"] = request.OpportunityWeights.SingleChannelDependency.ToString(),
            ["scoring_threshold_strong_channel_min"] = request.SignalThresholds.StrongChannelMin.ToString(),
            ["scoring_threshold_weak_channel_max"] = request.SignalThresholds.WeakChannelMax.ToString(),
            ["scoring_threshold_active_channel_min"] = request.SignalThresholds.ActiveChannelMin.ToString(),
            ["scoring_intent_low_max"] = request.Thresholds.LowMax.ToString(),
            ["scoring_intent_medium_max"] = request.Thresholds.MediumMax.ToString()
        };

        await UpsertSettingsAsync(settings, cancellationToken);
    }

    public async Task UpdateAutomationAsync(UpdateAdminLeadIntelligenceAutomationSettingsRequest request, CancellationToken cancellationToken)
    {
        ValidateAutomation(request);
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["automation_enabled"] = request.Enabled.ToString().ToLowerInvariant(),
            ["automation_refresh_interval_minutes"] = request.RefreshIntervalMinutes.ToString(),
            ["automation_batch_size"] = request.BatchSize.ToString(),
            ["automation_run_on_startup"] = request.RunOnStartup.ToString().ToLowerInvariant(),
            ["automation_enable_paid_media_evidence_sync"] = request.EnablePaidMediaEvidenceSync.ToString().ToLowerInvariant(),
            ["automation_paid_media_sync_interval_minutes"] = request.PaidMediaSyncIntervalMinutes.ToString()
        };

        await UpsertSettingsAsync(settings, cancellationToken);
    }

    private async Task UpsertSettingsAsync(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken)
    {
        var keys = settings.Keys.ToArray();
        var existing = await _db.LeadIntelligenceSettings
            .Where(x => keys.Contains(x.SettingKey))
            .ToDictionaryAsync(x => x.SettingKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var pair in settings)
        {
            if (!existing.TryGetValue(pair.Key, out var entity))
            {
                entity = new LeadIntelligenceSetting
                {
                    SettingKey = pair.Key,
                    UpdatedAt = now
                };
                _db.LeadIntelligenceSettings.Add(entity);
            }

            entity.SettingValue = pair.Value;
            entity.Description = SettingDescriptions.TryGetValue(pair.Key, out var description) ? description : entity.Description;
            entity.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateScoring(UpdateAdminLeadScoringSettingsRequest request)
    {
        RequireRange(request.BaseScore, 0, 100, "Base score");
        RequireRange(request.ActivityWeights.PromoActive, 0, 100, "Promo active weight");
        RequireRange(request.ActivityWeights.MetaStrong, 0, 100, "Meta strong weight");
        RequireRange(request.ActivityWeights.WebsiteActive, 0, 100, "Website active weight");
        RequireRange(request.ActivityWeights.MultiChannelPresence, 0, 100, "Multi-channel presence weight");
        RequireRange(request.OpportunityWeights.DigitalStrongButSearchWeak, 0, 100, "Digital-strong/search-weak weight");
        RequireRange(request.OpportunityWeights.DigitalStrongButOohWeak, 0, 100, "Digital-strong/OOH-weak weight");
        RequireRange(request.OpportunityWeights.PromoHeavyButBrandPresenceWeak, 0, 100, "Promo-heavy/brand-presence-weak weight");
        RequireRange(request.OpportunityWeights.SingleChannelDependency, 0, 100, "Single-channel-dependency weight");
        RequireRange(request.SignalThresholds.WeakChannelMax, 0, 99, "Weak channel max threshold");
        RequireRange(request.SignalThresholds.ActiveChannelMin, 1, 100, "Active channel min threshold");
        RequireRange(request.SignalThresholds.StrongChannelMin, 1, 100, "Strong channel min threshold");
        RequireRange(request.Thresholds.LowMax, 0, 99, "Low intent max threshold");
        RequireRange(request.Thresholds.MediumMax, 1, 100, "Medium intent max threshold");

        if (request.SignalThresholds.WeakChannelMax >= request.SignalThresholds.ActiveChannelMin)
        {
            throw new InvalidOperationException("Weak channel max must be lower than active channel min.");
        }

        if (request.SignalThresholds.ActiveChannelMin > request.SignalThresholds.StrongChannelMin)
        {
            throw new InvalidOperationException("Active channel min cannot exceed strong channel min.");
        }

        if (request.Thresholds.LowMax >= request.Thresholds.MediumMax)
        {
            throw new InvalidOperationException("Low intent max must be lower than medium intent max.");
        }
    }

    private static void ValidateAutomation(UpdateAdminLeadIntelligenceAutomationSettingsRequest request)
    {
        RequireRange(request.RefreshIntervalMinutes, 1, 1440, "Refresh interval");
        RequireRange(request.BatchSize, 1, 10000, "Batch size");
        RequireRange(request.PaidMediaSyncIntervalMinutes, 1, 10080, "Paid media sync interval");
    }

    private static void RequireRange(int value, int min, int max, string label)
    {
        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{label} must be between {min} and {max}.");
        }
    }
}
