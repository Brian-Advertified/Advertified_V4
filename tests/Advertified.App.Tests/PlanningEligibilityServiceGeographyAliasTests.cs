using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Services;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class PlanningEligibilityServiceGeographyAliasTests
{
    [Fact]
    public void FilterEligibleCandidates_TreatsSowetoAsJohannesburgForLocalMatching()
    {
        var policyService = new PlanningPolicyService(new PlanningPolicySnapshotProvider(new PlanningPolicyOptions()));
        var service = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService());
        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 50000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" }
        };

        var candidates = new List<InventoryCandidate>
        {
            new()
            {
                SourceId = Guid.NewGuid(),
                SourceType = "ooh",
                DisplayName = "Soweto Billboard",
                MediaType = "OOH",
                City = "Soweto",
                Area = "Soweto",
                Cost = 10000m,
                IsAvailable = true
            }
        };

        var result = service.FilterEligibleCandidates(candidates, request);
        result.Candidates.Should().HaveCount(1);
    }
}
