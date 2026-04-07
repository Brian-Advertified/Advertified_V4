namespace Advertified.App.Services.Abstractions;

public interface ILeadScoreService
{
    Task<LeadScoreResult> ScoreAsync(int leadId, CancellationToken cancellationToken);
}
