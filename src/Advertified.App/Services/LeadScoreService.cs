using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class LeadScoreService : ILeadScoreService
{
    private readonly AppDbContext _db;
    private readonly LeadScoringSettingsSnapshotProvider _settingsSnapshotProvider;
    private readonly ILeadChannelDetectionService _leadChannelDetectionService;

    public LeadScoreService(
        AppDbContext db,
        LeadScoringSettingsSnapshotProvider settingsSnapshotProvider,
        ILeadChannelDetectionService leadChannelDetectionService)
    {
        _db = db;
        _settingsSnapshotProvider = settingsSnapshotProvider;
        _leadChannelDetectionService = leadChannelDetectionService;
    }

    public async Task<LeadScoreResult> ScoreAsync(int leadId, CancellationToken cancellationToken)
    {
        var options = _settingsSnapshotProvider.GetCurrent();
        var lead = await _db.Leads
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == leadId, cancellationToken)
            ?? throw new InvalidOperationException("Lead not found.");

        var latestSignal = await _db.Signals
            .AsNoTracking()
            .Where(x => x.LeadId == leadId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var signalEvidences = latestSignal is null
            ? Array.Empty<LeadSignalEvidence>()
            : await _db.LeadSignalEvidences
                .AsNoTracking()
                .Where(item => item.SignalId == latestSignal.Id)
                .OrderByDescending(item => item.CreatedAt)
                .ToArrayAsync(cancellationToken);

        var channelScores = _leadChannelDetectionService
            .Detect(lead, latestSignal, signalEvidences)
            .ToDictionary(item => item.Channel, item => item.Score, StringComparer.OrdinalIgnoreCase);

        var activityScore = 0;
        if (latestSignal is not null)
        {
            if (latestSignal.HasPromo)
            {
                activityScore += options.ActivityWeights.PromoActive;
            }

            if (channelScores.TryGetValue("social", out var socialScore)
                && socialScore >= options.SignalThresholds.StrongChannelMin)
            {
                activityScore += options.ActivityWeights.MetaStrong;
            }

            if (latestSignal.WebsiteUpdatedRecently)
            {
                activityScore += options.ActivityWeights.WebsiteActive;
            }

            var activeChannelCount = channelScores.Count(score => score.Value >= options.SignalThresholds.ActiveChannelMin);
            if (activeChannelCount >= 2)
            {
                activityScore += options.ActivityWeights.MultiChannelPresence;
            }
        }

        activityScore = Math.Clamp(activityScore, 0, 50);

        var opportunityScore = 0;
        var digitalStrong = IsChannelStrong(channelScores, "social", options.SignalThresholds.StrongChannelMin)
            || IsChannelStrong(channelScores, "search", options.SignalThresholds.StrongChannelMin);
        var searchWeak = IsChannelWeak(channelScores, "search", options.SignalThresholds.WeakChannelMax);
        var oohWeak = IsChannelWeak(channelScores, "billboards_ooh", options.SignalThresholds.WeakChannelMax);
        var radioWeak = IsChannelWeak(channelScores, "radio", options.SignalThresholds.WeakChannelMax);
        var tvWeak = IsChannelWeak(channelScores, "tv", options.SignalThresholds.WeakChannelMax);
        var broadReachWeak = oohWeak && radioWeak && tvWeak;

        if (digitalStrong && searchWeak)
        {
            opportunityScore += options.OpportunityWeights.DigitalStrongButSearchWeak;
        }

        if (digitalStrong && oohWeak)
        {
            opportunityScore += options.OpportunityWeights.DigitalStrongButOohWeak;
        }

        if (latestSignal?.HasPromo == true && broadReachWeak)
        {
            opportunityScore += options.OpportunityWeights.PromoHeavyButBrandPresenceWeak;
        }

        var activeChannelCountForDependency = channelScores.Count(score => score.Value >= options.SignalThresholds.ActiveChannelMin);
        if (activeChannelCountForDependency == 1)
        {
            opportunityScore += options.OpportunityWeights.SingleChannelDependency;
        }

        opportunityScore = Math.Clamp(opportunityScore, 0, 50);
        var score = Math.Clamp(options.BaseScore + activityScore + opportunityScore, 0, 100);

        return new LeadScoreResult
        {
            LeadId = leadId,
            Score = score,
            IntentLevel = ResolveIntentLevel(score, options.Thresholds)
        };
    }

    private static bool IsChannelStrong(IReadOnlyDictionary<string, int> channelScores, string channel, int strongChannelMin)
    {
        return channelScores.TryGetValue(channel, out var score) && score >= strongChannelMin;
    }

    private static bool IsChannelWeak(IReadOnlyDictionary<string, int> channelScores, string channel, int weakChannelMax)
    {
        return !channelScores.TryGetValue(channel, out var score) || score <= weakChannelMax;
    }

    private static string ResolveIntentLevel(int score, LeadIntentThresholds thresholds)
    {
        if (score <= thresholds.LowMax)
        {
            return "Low";
        }

        if (score <= thresholds.MediumMax)
        {
            return "Medium";
        }

        return "High";
    }
}
