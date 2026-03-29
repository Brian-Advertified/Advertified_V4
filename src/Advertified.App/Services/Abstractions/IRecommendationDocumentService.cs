namespace Advertified.App.Services.Abstractions;

public interface IRecommendationDocumentService
{
    Task<byte[]> GetCampaignPdfBytesAsync(Guid campaignId, CancellationToken cancellationToken);
}
