using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface IProspectLeadRegistrationService
{
    Task<ProspectLeadRegistrationResult> UpsertAgentLeadAsync(
        Guid agentUserId,
        string fullName,
        string email,
        string phone,
        string source,
        CancellationToken cancellationToken);

    Task<ProspectLeadRegistrationResult> UpsertPublicLeadAsync(
        string fullName,
        string email,
        string phone,
        string source,
        CancellationToken cancellationToken);
}

public sealed record ProspectLeadRegistrationResult(
    ProspectLead Lead,
    bool CreatedNewLead,
    string NormalizedEmail,
    string NormalizedPhone);
