using Advertified.App.Campaigns;

namespace Advertified.App.Services.Abstractions;

internal interface IRecommendationProposalIntelligenceService
{
    RecommendationProposalIntelligenceResult Build(RecommendationProposalIntelligenceRequest request);
}
