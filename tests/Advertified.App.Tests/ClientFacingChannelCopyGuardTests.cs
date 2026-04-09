namespace Advertified.App.Tests;

public class ClientFacingChannelCopyGuardTests
{
    [Fact]
    public void LeadIntelligenceEvidenceLine_UsesClientSafeBillboardsLabel()
    {
        var file = ReadRepositoryFile("src", "Advertified.Web", "src", "features", "leads", "leadIntelligenceViewModel.ts");

        Assert.DoesNotContain("OOH channel score:", file, StringComparison.Ordinal);
        Assert.Contains("Billboards and Digital Screens channel score:", file, StringComparison.Ordinal);
    }

    [Fact]
    public void ControllerChannelLabelMapping_UsesClientSafeBillboardsLabel()
    {
        var file = ReadRepositoryFile("src", "Advertified.App", "Controllers", "ControllerMappings.cs");

        Assert.DoesNotContain("\"ooh\" => \"OOH\"", file, StringComparison.Ordinal);
        Assert.Contains("\"ooh\" => \"Billboards and Digital Screens\"", file, StringComparison.Ordinal);
    }

    [Fact]
    public void LeadProposalPlaybookCopy_DoesNotExposeRawOohTerm()
    {
        var file = ReadRepositoryFile("src", "Advertified.App", "Services", "RecommendationDocumentService.cs");
        var playbookBody = ExtractPlaybookBody(file);

        Assert.DoesNotMatch(@"\bOOH\b", playbookBody);
        Assert.Contains("Billboards and Digital Screens", playbookBody, StringComparison.Ordinal);
    }

    private static string ExtractPlaybookBody(string source)
    {
        const string marker = "private static (string Label, string Strategy)? ResolveArchetypeProposalPlaybook";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var segment = source[start..];
        var endMarker = "private static string GetProposalLetter";
        var end = segment.IndexOf(endMarker, StringComparison.Ordinal);
        return end > 0 ? segment[..end] : segment;
    }

    private static string ReadRepositoryFile(params string[] pathSegments)
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var path = Path.Combine(new[] { repositoryRoot }.Concat(pathSegments).ToArray());
        return File.ReadAllText(path);
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var guardrailsCandidate = Path.Combine(current.FullName, "engineering_guardrails.md");
            var sourceCandidate = Path.Combine(current.FullName, "src");
            if (File.Exists(guardrailsCandidate) && Directory.Exists(sourceCandidate))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test execution path.");
    }
}
