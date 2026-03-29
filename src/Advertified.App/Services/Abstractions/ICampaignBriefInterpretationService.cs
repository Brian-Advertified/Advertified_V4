using Advertified.App.Contracts.Agent;

namespace Advertified.App.Services.Abstractions;

public interface ICampaignBriefInterpretationService
{
    Task<InterpretedCampaignBriefResponse> InterpretAsync(
        InterpretCampaignBriefRequest request,
        CancellationToken cancellationToken);
}
