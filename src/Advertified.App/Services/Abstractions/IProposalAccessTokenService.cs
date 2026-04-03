namespace Advertified.App.Services.Abstractions;

public interface IProposalAccessTokenService
{
    string CreateToken(Guid campaignId);
    bool TryReadToken(string token, out ProposalAccessTokenPayload payload);
}

public sealed record ProposalAccessTokenPayload(Guid CampaignId, DateTime IssuedAtUtc);
