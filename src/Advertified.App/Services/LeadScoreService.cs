using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class LeadScoreService : ILeadScoreService
{
    private readonly AppDbContext _db;
    private readonly LeadScoringOptions _options;

    public LeadScoreService(AppDbContext db, IOptions<LeadScoringOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<LeadScoreResult> ScoreAsync(int leadId, CancellationToken cancellationToken)
    {
        var leadExists = await _db.Leads
            .AsNoTracking()
            .AnyAsync(x => x.Id == leadId, cancellationToken);

        if (!leadExists)
        {
            throw new InvalidOperationException("Lead not found.");
        }

        var latestSignal = await _db.Signals
            .AsNoTracking()
            .Where(x => x.LeadId == leadId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var score = _options.BaseScore;
        if (latestSignal is not null)
        {
            if (latestSignal.HasPromo)
            {
                score += _options.Weights.HasPromo;
            }

            if (latestSignal.HasMetaAds)
            {
                score += _options.Weights.HasMetaAds;
            }

            if (latestSignal.WebsiteUpdatedRecently)
            {
                score += _options.Weights.WebsiteUpdatedRecently;
            }
        }

        return new LeadScoreResult
        {
            LeadId = leadId,
            Score = score,
            IntentLevel = ResolveIntentLevel(score)
        };
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
