using Advertified.App.Contracts.Admin;

namespace Advertified.App.Services.Abstractions;

public interface IAdminLeadIntelligenceSettingsService
{
    Task<AdminLeadIntelligenceSettingsResponse> GetCurrentAsync(CancellationToken cancellationToken);

    Task UpdateScoringAsync(UpdateAdminLeadScoringSettingsRequest request, CancellationToken cancellationToken);

    Task UpdateAutomationAsync(UpdateAdminLeadIntelligenceAutomationSettingsRequest request, CancellationToken cancellationToken);
}
