namespace Advertified.App.Services.Abstractions;

public interface ILeadIntelligenceOrchestrator
{
    Task<LeadIntelligenceRunResult> RunLeadAsync(int leadId, CancellationToken cancellationToken);

    Task<int> RunAllAsync(CancellationToken cancellationToken);
}
