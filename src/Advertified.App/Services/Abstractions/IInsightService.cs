using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface IInsightService
{
    Task<string> GenerateInsightAsync(
        Lead lead,
        Signal? previousSignal,
        Signal currentSignal,
        LeadScoreResult score,
        LeadTrendAnalysisResult trend,
        CancellationToken cancellationToken);
}
