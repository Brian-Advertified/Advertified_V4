using Advertified.App.Campaigns;
using FluentAssertions;

public sealed class PdfPreviewExportTests
{
    [Fact]
    public void GenerateLeadSamplePdf_WritesDemoFile()
    {
        var bytes = RecommendationPdfPreviewFactory.GenerateLeadSample();
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".tmp", "demo-pdfs");
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, "lead-recommendation-demo.pdf");
        File.WriteAllBytes(outputPath, bytes);

        File.Exists(outputPath).Should().BeTrue();
        new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateRecommendationPdf_AllowsLargeProposalSectionsToSpanMultiplePages()
    {
        var items = Enumerable.Range(1, 18)
            .Select(index => new RecommendationLineDocumentModel
            {
                Channel = index % 2 == 0 ? "Radio" : "OOH",
                Title = $"Placement {index}",
                Rationale = "Large test placement rationale.",
                TotalCost = 10000m + index,
                Quantity = 1,
                Region = "Gauteng",
                Duration = "4 weeks",
                SlotType = "Test slot",
                Restrictions = "Subject to availability.",
                SelectionReasons = new[] { "Audience fit", "Budget fit", "Coverage fit" }
            })
            .ToArray();

        var model = new RecommendationDocumentModel
        {
            ClientName = "Demo Client",
            BusinessName = "Demo Business",
            CampaignName = "Large Recommendation Pack",
            PackageName = "Scale",
            SelectedBudget = 250000m,
            BudgetDisplayText = "R 250,000",
            GeneratedAtUtc = DateTime.UtcNow,
            CampaignObjective = "Drive awareness",
            TargetAreas = new[] { "Gauteng" },
            TargetLanguages = new[] { "English" },
            Proposals = new[]
            {
                new RecommendationProposalDocumentModel
                {
                    Label = "Proposal A",
                    Strategy = "Balanced mix",
                    Summary = "Summary",
                    Rationale = "Rationale",
                    TotalCost = 180000m,
                    Items = items
                },
                new RecommendationProposalDocumentModel
                {
                    Label = "Proposal B",
                    Strategy = "OOH-led reach",
                    Summary = "Summary",
                    Rationale = "Rationale",
                    TotalCost = 220000m,
                    Items = items
                },
                new RecommendationProposalDocumentModel
                {
                    Label = "Proposal C",
                    Strategy = "Radio-led frequency",
                    Summary = "Summary",
                    Rationale = "Rationale",
                    TotalCost = 250000m,
                    Items = items
                }
            }
        };

        var act = () => RecommendationPdfGenerator.Generate(model, null);

        act.Should().NotThrow();
        act().Length.Should().BeGreaterThan(0);
    }
}
