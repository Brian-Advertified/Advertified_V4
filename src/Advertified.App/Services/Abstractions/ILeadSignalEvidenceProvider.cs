using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ILeadSignalEvidenceProvider
{
    Task<IReadOnlyList<LeadSignalEvidenceInput>> CollectAsync(Lead lead, CancellationToken cancellationToken);
}
