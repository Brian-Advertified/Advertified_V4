namespace Advertified.App.Services.Abstractions;

public interface IRecommendationDocumentService
{
    Task<byte[]> GetCampaignPdfBytesAsync(Guid campaignId, CancellationToken cancellationToken);
    Task<byte[]> GetRecommendationPdfBytesAsync(Guid campaignId, Guid recommendationId, CancellationToken cancellationToken);
}
