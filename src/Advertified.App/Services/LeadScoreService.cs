using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class LeadScoreService : ILeadScoreService
{
    private readonly AppDbContext _db;
    private readonly LeadScoringOptions _options;
    private readonly ILeadChannelDetectionService _leadChannelDetectionService;

    public LeadScoreService(
        AppDbContext db,
        IOptions<LeadScoringOptions> options,
        ILeadChannelDetectionService leadChannelDetectionService)
    {
        _db = db;
        _options = options.Value;
        _leadChannelDetectionService = leadChannelDetectionService;
    }

    public async Task<LeadScoreResult> ScoreAsync(int leadId, CancellationToken cancellationToken)
    {
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
                activityScore += _options.ActivityWeights.PromoActive;
            }

            if (channelScores.TryGetValue("social", out var socialScore)
                && socialScore >= _options.SignalThresholds.StrongChannelMin)
            {
                activityScore += _options.ActivityWeights.MetaStrong;
            }

            if (latestSignal.WebsiteUpdatedRecently)
            {
                activityScore += _options.ActivityWeights.WebsiteActive;
            }

            var activeChannelCount = channelScores.Count(score => score.Value >= _options.SignalThresholds.ActiveChannelMin);
            if (activeChannelCount >= 2)
            {
                activityScore += _options.ActivityWeights.MultiChannelPresence;
            }
        }

        activityScore = Math.Clamp(activityScore, 0, 50);

        var opportunityScore = 0;
        var digitalStrong = IsChannelStrong(channelScores, "social") || IsChannelStrong(channelScores, "search");
        var searchWeak = IsChannelWeak(channelScores, "search");
        var oohWeak = IsChannelWeak(channelScores, "billboards_ooh");
        var radioWeak = IsChannelWeak(channelScores, "radio");
        var tvWeak = IsChannelWeak(channelScores, "tv");
        var broadReachWeak = oohWeak && radioWeak && tvWeak;

        if (digitalStrong && searchWeak)
        {
            opportunityScore += _options.OpportunityWeights.DigitalStrongButSearchWeak;
        }

        if (digitalStrong && oohWeak)
        {
            opportunityScore += _options.OpportunityWeights.DigitalStrongButOohWeak;
        }

        if (latestSignal?.HasPromo == true && broadReachWeak)
        {
            opportunityScore += _options.OpportunityWeights.PromoHeavyButBrandPresenceWeak;
        }

        var activeChannelCountForDependency = channelScores.Count(score => score.Value >= _options.SignalThresholds.ActiveChannelMin);
        if (activeChannelCountForDependency == 1)
        {
            opportunityScore += _options.OpportunityWeights.SingleChannelDependency;
        }

        opportunityScore = Math.Clamp(opportunityScore, 0, 50);
        var score = Math.Clamp(_options.BaseScore + activityScore + opportunityScore, 0, 100);

        return new LeadScoreResult
        {
            LeadId = leadId,
            Score = score,
            IntentLevel = ResolveIntentLevel(score)
        };
    }

    private bool IsChannelStrong(IReadOnlyDictionary<string, int> channelScores, string channel)
    {
        return channelScores.TryGetValue(channel, out var score) && score >= _options.SignalThresholds.StrongChannelMin;
    }

    private bool IsChannelWeak(IReadOnlyDictionary<string, int> channelScores, string channel)
    {
        return !channelScores.TryGetValue(channel, out var score) || score <= _options.SignalThresholds.WeakChannelMax;
    }

    private string ResolveIntentLevel(int score)
    {
        if (score <= _options.Thresholds.LowMax)
        {
            return "Low";
        }

        if (score <= _options.Thresholds.MediumMax)
        {
            return "Medium";
        }

        return "High";
    }
}
