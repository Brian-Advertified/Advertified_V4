using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Advertified.App.Configuration;

namespace Advertified.App.Services;

public sealed class LeadIntelligenceOrchestrator : ILeadIntelligenceOrchestrator
{
    private readonly AppDbContext _db;
    private readonly ISignalCollectorService _signalCollectorService;
    private readonly ILeadScoreService _leadScoreService;
    private readonly ITrendAnalysisService _trendAnalysisService;
    private readonly IInsightService _insightService;
    private readonly ILeadActionRecommendationService _leadActionRecommendationService;
    private readonly ILogger<LeadIntelligenceOrchestrator> _logger;
    private readonly LeadIntelligenceAutomationOptions _options;

    public LeadIntelligenceOrchestrator(
        AppDbContext db,
        ISignalCollectorService signalCollectorService,
        ILeadScoreService leadScoreService,
        ITrendAnalysisService trendAnalysisService,
        IInsightService insightService,
        ILeadActionRecommendationService leadActionRecommendationService,
        IOptions<LeadIntelligenceAutomationOptions> options,
        ILogger<LeadIntelligenceOrchestrator> logger)
    {
        _db = db;
        _signalCollectorService = signalCollectorService;
        _leadScoreService = leadScoreService;
        _trendAnalysisService = trendAnalysisService;
        _insightService = insightService;
        _leadActionRecommendationService = leadActionRecommendationService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LeadIntelligenceRunResult> RunLeadAsync(int leadId, CancellationToken cancellationToken)
    {
        var lead = await _db.Leads
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == leadId, cancellationToken)
            ?? throw new InvalidOperationException("Lead not found.");

        var previousSignal = await _db.Signals
            .AsNoTracking()
            .Where(x => x.LeadId == leadId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var signal = await _signalCollectorService.CollectAsync(leadId, cancellationToken);
        var score = await _leadScoreService.ScoreAsync(leadId, cancellationToken);
        var trend = _trendAnalysisService.Analyze(previousSignal, signal);
        var insightText = await _insightService.GenerateInsightAsync(lead, previousSignal, signal, score, trend, cancellationToken);

        var insight = new LeadInsight
        {
            LeadId = leadId,
            SignalId = signal.Id,
            TrendSummary = trend.Summary,
            ScoreSnapshot = score.Score,
            IntentLevelSnapshot = score.IntentLevel,
            Text = insightText,
            CreatedAt = DateTime.UtcNow,
        };

        _db.LeadInsights.Add(insight);
        await _db.SaveChangesAsync(cancellationToken);

        var recommendedActions = _leadActionRecommendationService
            .BuildRecommendedActions(lead, score, trend, insight)
            .ToList();

        foreach (var action in recommendedActions)
        {
            var existingOpenAction = await _db.LeadActions.FirstOrDefaultAsync(
                x => x.LeadId == leadId
                    && x.ActionType == action.ActionType
                    && x.Status == "open",
                cancellationToken);

            if (existingOpenAction is not null)
            {
                continue;
            }

            _db.LeadActions.Add(action);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new LeadIntelligenceRunResult
        {
            Lead = lead,
            Signal = signal,
            Score = score,
            Insight = insight,
            RecommendedActions = recommendedActions,
        };
    }

    public async Task<int> RunAllAsync(CancellationToken cancellationToken)
    {
        var leadIds = await _db.Leads
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Take(Math.Max(1, _options.BatchSize))
            .ToListAsync(cancellationToken);

        var processedCount = 0;
        foreach (var leadId in leadIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await RunLeadAsync(leadId, cancellationToken);
                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lead intelligence refresh failed for lead {LeadId}.", leadId);
            }
        }

        return processedCount;
    }
}
