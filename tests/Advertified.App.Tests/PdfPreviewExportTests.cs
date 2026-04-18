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
}
