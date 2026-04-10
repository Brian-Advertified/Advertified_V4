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

    [Fact]
    public void FilterEligibleCandidates_WhenSuburbsSpecified_RequiresSuburbMatchWithinRequestedCity()
    {
        var policyService = new PlanningPolicyService(new PlanningPolicySnapshotProvider(new PlanningPolicyOptions()));
        var service = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService());
        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 100000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            Suburbs = new List<string> { "DiepKloof, Soweto" }
        };

        var matchingCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "DiepKloof Billboard",
            MediaType = "OOH",
            City = "Johannesburg",
            Suburb = "DiepKloof, Soweto",
            Area = "DiepKloof, Soweto",
            Cost = 45000m,
            IsAvailable = true
        };

        var nonMatchingSuburbSameCity = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Rosebank Billboard",
            MediaType = "OOH",
            City = "Johannesburg",
            Suburb = "Rosebank",
            Area = "Rosebank",
            Cost = 45000m,
            IsAvailable = true
        };

        var suburbMatchWrongCity = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Pretoria DiepKloof Billboard",
            MediaType = "OOH",
            City = "Pretoria",
            Suburb = "DiepKloof, Soweto",
            Area = "DiepKloof, Soweto",
            Cost = 45000m,
            IsAvailable = true
        };

        var result = service.FilterEligibleCandidates(new List<InventoryCandidate>
        {
            matchingCandidate,
            nonMatchingSuburbSameCity,
            suburbMatchWrongCity
        }, request);

        result.Candidates.Should().ContainSingle(x => x.DisplayName == "DiepKloof Billboard");
    }
}
