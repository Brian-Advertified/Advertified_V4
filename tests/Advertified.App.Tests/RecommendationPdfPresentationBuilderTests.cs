using Advertified.App.Campaigns;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class RecommendationPdfPresentationBuilderTests
{
    [Fact]
    public void BuildClientSelectionSummary_IncludesReachWithoutRepeatingRegion()
    {
        var model = new RecommendationDocumentModel
        {
            TargetAudienceSummary = "Retail shoppers"
        };

        var item = new RecommendationLineDocumentModel
        {
            Channel = "OOH",
            Title = "CBD, Johannesburg",
            Region = "CBD, Johannesburg, Gauteng",
            TrafficCount = "125000",
            SelectionReasons = new[] { "Strong geography match" }
        };

        var lines = RecommendationPdfPresentationBuilder.BuildClientSelectionSummary(model, item);

        lines.Should().Contain("Who we are targeting: Retail shoppers");
        lines.Should().Contain("Estimated audience size: Approximately 125 000 people pass this site.");
        lines.Should().NotContain(line => line.Contains("CBD, Johannesburg, Gauteng", StringComparison.OrdinalIgnoreCase));
    }
}
