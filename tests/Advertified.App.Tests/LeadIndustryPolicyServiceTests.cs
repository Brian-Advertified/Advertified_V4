using Advertified.App.Configuration;
using Advertified.App.Services;
using FluentAssertions;

namespace Advertified.App.Tests;

public class LeadIndustryPolicyServiceTests
{
    [Fact]
    public void ResolveForCategory_UsesConfiguredPolicyProfiles()
    {
        var provider = new LeadIndustryPolicySnapshotProvider(new LeadIndustryPolicyOptions
        {
            Profiles = new List<LeadIndustryPolicyProfileOptions>
            {
                new LeadIndustryPolicyProfileOptions
                {
                    Key = "retail",
                    Name = "Retail",
                    ObjectiveOverride = "promotion",
                    PreferredTone = "performance",
                    PreferredChannels = new List<string> { "OOH", "Digital", "Digital" },
                    Cta = "Visit us today",
                    MessagingAngle = "local visibility",
                    Guardrails = new List<string> { "Stay on-brand" },
                    AdditionalGap = "Gap",
                    AdditionalOutcome = "Outcome",
                },
                new LeadIndustryPolicyProfileOptions
                {
                    Key = "default",
                    Name = "Default",
                    PreferredChannels = new List<string> { "Digital" },
                    Cta = "Contact us",
                    MessagingAngle = "clear action",
                    Guardrails = new List<string> { "Be clear" },
                    AdditionalGap = "Default gap",
                    AdditionalOutcome = "Default outcome",
                },
            },
        });

        var service = new LeadIndustryPolicyService(provider);

        var result = service.ResolveForCategory("retail");

        result.Key.Should().Be("retail");
        result.ObjectiveOverride.Should().Be("promotion");
        result.PreferredChannels.Should().Equal("OOH", "Digital");
    }

    [Fact]
    public void ResolveForCategory_FallsBackToDefaultProfile()
    {
        var provider = new LeadIndustryPolicySnapshotProvider(new LeadIndustryPolicyOptions
        {
            Profiles = new List<LeadIndustryPolicyProfileOptions>
            {
                new LeadIndustryPolicyProfileOptions
                {
                    Key = "default",
                    Name = "Default",
                    PreferredChannels = new List<string> { "Digital" },
                    Cta = "Contact us",
                    MessagingAngle = "clear action",
                    Guardrails = new List<string> { "Be clear" },
                    AdditionalGap = "Default gap",
                    AdditionalOutcome = "Default outcome",
                },
            },
        });

        var service = new LeadIndustryPolicyService(provider);

        var result = service.ResolveForCategory("unknown-category");

        result.Key.Should().Be("default");
        result.Name.Should().Be("Default");
    }
}
