using System.Net;
using System.Net.Http.Headers;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Leads;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Tests;

public class WebsiteSignalProviderTests
{
    [Fact]
    public async Task CollectAsync_DetectsPromoMetaAdsAndRecentWebsiteUpdate()
    {
        var html = @"
            <html>
              <body>
                <a href=""/specials"">Winter special</a>
                <script src=""https://connect.facebook.net/en_US/fbevents.js""></script>
              </body>
            </html>
            ";

        var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };
            response.Content.Headers.LastModified = DateTimeOffset.UtcNow.AddDays(-2);
            return response;
        }));

        var provider = new WebsiteSignalProvider(client);

        var result = await provider.CollectAsync("https://example.com", CancellationToken.None);

        result.HasPromo.Should().BeTrue();
        result.HasMetaAds.Should().BeTrue();
        result.WebsiteUpdatedRecently.Should().BeTrue();
    }

    [Fact]
    public async Task CollectAsync_UsesRequestedPathWhenHtmlIsSparse()
    {
        var client = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>hello</body></html>")
        }));

        var provider = new WebsiteSignalProvider(client);

        var result = await provider.CollectAsync("https://example.com/offers", CancellationToken.None);

        result.HasPromo.Should().BeTrue();
    }

    [Fact]
    public async Task CollectAsync_ReturnsEmptySignalsWhenRequestFails()
    {
        var client = new HttpClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("boom")));
        var provider = new WebsiteSignalProvider(client);

        var result = await provider.CollectAsync("https://example.com", CancellationToken.None);

        result.Should().BeEquivalentTo(new WebsiteSignalResult());
    }
}

public class LeadScoreServiceTests
{
    [Fact]
    public async Task ScoreAsync_UsesLatestSignalForTheLead()
    {
        var db = LeadIntelligenceTestHelpers.CreateDbContext();
        db.Leads.Add(new Lead
        {
            Id = 7,
            Name = "Fit Lab",
            Location = "Johannesburg",
            Category = "Fitness",
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        });
        db.Signals.AddRange(
            new Signal
            {
                Id = 1,
                LeadId = 7,
                HasPromo = false,
                HasMetaAds = false,
                WebsiteUpdatedRecently = false,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new Signal
            {
                Id = 2,
                LeadId = 7,
                HasPromo = true,
                HasMetaAds = true,
                WebsiteUpdatedRecently = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
        await db.SaveChangesAsync();

        var service = new LeadScoreService(db, Options.Create(new LeadScoringOptions
        {
            BaseScore = 10,
            Weights = new LeadSignalScoringWeights
            {
                HasPromo = 30,
                HasMetaAds = 40,
                WebsiteUpdatedRecently = 20
            },
            Thresholds = new LeadIntentThresholds
            {
                LowMax = 40,
                MediumMax = 70
            }
        }));

        var result = await service.ScoreAsync(7, CancellationToken.None);

        result.Score.Should().Be(100);
        result.IntentLevel.Should().Be("High");
    }
}

public class LeadSourceImportServiceTests
{
    [Fact]
    public async Task ImportCsvAsync_MapsStandardCsvRowsIntoLeads()
    {
        await using var db = LeadIntelligenceTestHelpers.CreateDbContext();
        var ingestionService = new LeadSourceIngestionService(db);
        var importService = new LeadSourceImportService(ingestionService);
        var csv = "name,website,location,category,source_reference\r\nFit Lab,fitlab.co.za,Johannesburg,Fitness,gmaps-001";

        var result = await importService.ImportCsvAsync(csv, "csv_import", "standard", CancellationToken.None);

        result.CreatedCount.Should().Be(1);
        result.Leads.Should().ContainSingle();
        result.Leads[0].Source.Should().Be("csv_import");
        result.Leads[0].SourceReference.Should().Be("gmaps-001");
    }

    [Fact]
    public async Task ImportCsvAsync_MapsGoogleMapsProfileUsingExportHeaders()
    {
        await using var db = LeadIntelligenceTestHelpers.CreateDbContext();
        var ingestionService = new LeadSourceIngestionService(db);
        var importService = new LeadSourceImportService(ingestionService);
        var csv = "business_name,website,city,main_category,place_id\r\nUrban Dental,,Cape Town,Dentist,ChIJ123";

        var result = await importService.ImportCsvAsync(csv, "google_maps", "google_maps", CancellationToken.None);

        result.CreatedCount.Should().Be(1);
        result.Leads.Should().ContainSingle();
        result.Leads[0].Name.Should().Be("Urban Dental");
        result.Leads[0].Location.Should().Be("Cape Town");
        result.Leads[0].Category.Should().Be("Dentist");
        result.Leads[0].Source.Should().Be("google_maps");
        result.Leads[0].SourceReference.Should().Be("ChIJ123");
    }
}

public class LeadSourceIngestionServiceTests
{
    [Fact]
    public async Task IngestAsync_ReusesExistingLeadWhenWebsiteDiffersOnlyBySchemeOrPath()
    {
        await using var db = LeadIntelligenceTestHelpers.CreateDbContext();
        db.Leads.Add(new Lead
        {
            Id = 41,
            Name = "Fit Lab",
            Website = "fitlab.co.za",
            Location = "Johannesburg",
            Category = "Fitness",
            Source = "google_maps",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        });
        await db.SaveChangesAsync();

        var service = new LeadSourceIngestionService(db);
        var result = await service.IngestAsync(
            new[]
            {
                new IngestLeadSourceItemRequest
                {
                    Name = "Fit Lab",
                    Website = "https://www.fitlab.co.za/offers?campaign=winter",
                    Location = "Johannesburg",
                    Category = "Fitness",
                    Source = "csv_drop"
                }
            },
            CancellationToken.None);

        result.CreatedCount.Should().Be(0);
        result.UpdatedCount.Should().Be(1);
        db.Leads.Should().ContainSingle();
    }

    [Fact]
    public async Task IngestAsync_NormalizesLocationCategoryAndWhitespaceForNameBasedDeduping()
    {
        await using var db = LeadIntelligenceTestHelpers.CreateDbContext();
        db.Leads.Add(new Lead
        {
            Id = 42,
            Name = "Urban Dental",
            Location = "Cape Town",
            Category = "Dentist",
            Source = "manual",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        });
        await db.SaveChangesAsync();

        var service = new LeadSourceIngestionService(db);
        var result = await service.IngestAsync(
            new[]
            {
                new IngestLeadSourceItemRequest
                {
                    Name = "  Urban   Dental  ",
                    Website = "",
                    Location = "Cape Town, Western Cape",
                    Category = "Dentist | Orthodontist",
                    Source = "google_maps"
                }
            },
            CancellationToken.None);

        result.CreatedCount.Should().Be(0);
        result.UpdatedCount.Should().Be(1);
        db.Leads.Should().ContainSingle();
        result.Leads[0].Location.Should().Be("Cape Town");
        result.Leads[0].Category.Should().Be("Dentist");
    }
}

public class TrendAnalysisServiceTests
{
    [Fact]
    public void Analyze_IdentifiesFreshCampaignSignals()
    {
        var service = new TrendAnalysisService();

        var result = service.Analyze(
            new Signal { HasPromo = false, HasMetaAds = false, WebsiteUpdatedRecently = false },
            new Signal { HasPromo = true, HasMetaAds = true, WebsiteUpdatedRecently = true });

        result.CampaignStartedRecently.Should().BeTrue();
        result.ActivityIncreased.Should().BeTrue();
        result.Summary.Should().Contain("Promotion activity just appeared.");
        result.Summary.Should().Contain("Meta ad activity was newly detected.");
    }

    [Fact]
    public void Analyze_ReportsNoMaterialChangeWhenSignalsAreStable()
    {
        var service = new TrendAnalysisService();

        var result = service.Analyze(
            new Signal { HasPromo = true, HasMetaAds = false, WebsiteUpdatedRecently = true },
            new Signal { HasPromo = true, HasMetaAds = false, WebsiteUpdatedRecently = true });

        result.CampaignStartedRecently.Should().BeFalse();
        result.ActivityIncreased.Should().BeFalse();
        result.Summary.Should().Be("No material signal change detected since the previous analysis.");
    }
}

public class LeadSourceDropFolderProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ImportsCsvAndMovesFileToProcessedFolder()
    {
        var root = LeadIntelligenceTestHelpers.CreateTempDirectory();
        try
        {
            var inboxPath = Path.Combine(root, "inbox");
            Directory.CreateDirectory(inboxPath);
            await File.WriteAllTextAsync(
                Path.Combine(inboxPath, "leads.csv"),
                "name,website,location,category\r\nFit Lab,fitlab.co.za,Johannesburg,Fitness");

            var importService = new StubLeadSourceImportService(new LeadSourceIngestionResult
            {
                Leads = new[]
                {
                    new Lead { Id = 25, Name = "Fit Lab", Location = "Johannesburg", Category = "Fitness" }
                }
            });
            var orchestrator = new StubLeadIntelligenceOrchestrator();
            var processor = new LeadSourceDropFolderProcessor(
                new LeadIntelligenceStubWebHostEnvironment(root),
                importService,
                orchestrator,
                Options.Create(new LeadSourceDropFolderOptions
                {
                    Enabled = true,
                    InboxPath = "inbox",
                    ProcessedPath = "processed",
                    FailedPath = "failed",
                    AnalyzeImportedLeads = true
                }),
                NullLogger<LeadSourceDropFolderProcessor>.Instance);

            var result = await processor.ProcessAsync(CancellationToken.None);

            result.ProcessedFileCount.Should().Be(1);
            result.FailedFileCount.Should().Be(0);
            result.ImportedLeadCount.Should().Be(1);
            result.AnalyzedLeadCount.Should().Be(1);
            importService.Calls.Should().ContainSingle();
            importService.Calls[0].DefaultSource.Should().Be("csv_drop");
            importService.Calls[0].ImportProfile.Should().Be("standard");
            orchestrator.ProcessedLeadIds.Should().ContainSingle().Which.Should().Be(25);
            Directory.GetFiles(Path.Combine(root, "processed"), "*.csv").Should().ContainSingle();
            Directory.GetFiles(Path.Combine(root, "inbox"), "*.csv").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessAsync_InfersGoogleMapsProfileFromFileName()
    {
        var root = LeadIntelligenceTestHelpers.CreateTempDirectory();
        try
        {
            var inboxPath = Path.Combine(root, "inbox");
            Directory.CreateDirectory(inboxPath);
            await File.WriteAllTextAsync(
                Path.Combine(inboxPath, "google-maps-export.csv"),
                "business_name,city,main_category,place_id\r\nUrban Dental,Cape Town,Dentist,ChIJ123");

            var importService = new StubLeadSourceImportService(new LeadSourceIngestionResult());
            var processor = new LeadSourceDropFolderProcessor(
                new LeadIntelligenceStubWebHostEnvironment(root),
                importService,
                new StubLeadIntelligenceOrchestrator(),
                Options.Create(new LeadSourceDropFolderOptions
                {
                    Enabled = true,
                    InboxPath = "inbox",
                    ProcessedPath = "processed",
                    FailedPath = "failed",
                    DefaultSource = "csv_drop",
                    DefaultImportProfile = "standard",
                    AnalyzeImportedLeads = false
                }),
                NullLogger<LeadSourceDropFolderProcessor>.Instance);

            await processor.ProcessAsync(CancellationToken.None);

            importService.Calls.Should().ContainSingle();
            importService.Calls[0].DefaultSource.Should().Be("google_maps");
            importService.Calls[0].ImportProfile.Should().Be("google_maps");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

public class LeadSourceAutomationStatusServiceTests
{
    [Fact]
    public async Task GetStatus_ReturnsResolvedPathsAndFileCounts()
    {
        var root = LeadIntelligenceTestHelpers.CreateTempDirectory();
        try
        {
            var inbox = Path.Combine(root, "inbox");
            var processed = Path.Combine(root, "processed");
            var failed = Path.Combine(root, "failed");
            Directory.CreateDirectory(inbox);
            Directory.CreateDirectory(processed);
            Directory.CreateDirectory(failed);

            await File.WriteAllTextAsync(Path.Combine(inbox, "pending.csv"), "name\r\nFit Lab");
            await File.WriteAllTextAsync(Path.Combine(processed, "done.csv"), "name\r\nFit Lab");
            await File.WriteAllTextAsync(Path.Combine(failed, "bad.csv"), "name\r\nFit Lab");

            var service = new LeadSourceAutomationStatusService(
                new LeadIntelligenceStubWebHostEnvironment(root),
                Options.Create(new LeadSourceDropFolderOptions
                {
                    Enabled = true,
                    InboxPath = "inbox",
                    ProcessedPath = "processed",
                    FailedPath = "failed",
                    DefaultSource = "csv_drop",
                    DefaultImportProfile = "google_maps",
                    AnalyzeImportedLeads = true
                }));

            var result = service.GetStatus();

            result.DropFolderEnabled.Should().BeTrue();
            result.PendingFileCount.Should().Be(1);
            result.ProcessedFileCount.Should().Be(1);
            result.FailedFileCount.Should().Be(1);
            result.DefaultSource.Should().Be("csv_drop");
            result.DefaultImportProfile.Should().Be("google_maps");
            result.AnalyzeImportedLeads.Should().BeTrue();
            result.InboxPath.Should().Be(inbox);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

public class LeadChannelDetectionServiceTests
{
    [Fact]
    public void Detect_ScoresSocialAsDetectedWhenMetaAdsArePresent()
    {
        var service = new LeadChannelDetectionService();
        var lead = new Lead
        {
            Id = 61,
            Name = "Fit Lab",
            Website = "fitlab.co.za",
            Location = "Johannesburg",
            Category = "Fitness"
        };
        var signal = new Signal
        {
            LeadId = 61,
            HasMetaAds = true,
            HasPromo = true,
            WebsiteUpdatedRecently = true,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var results = service.Detect(lead, signal);
        var social = results.Single(x => x.Channel == "social");

        social.Score.Should().BeGreaterThanOrEqualTo(80);
        social.Confidence.Should().Be("detected");
        social.Status.Should().Be("evidence_found");
    }

    [Fact]
    public void Detect_KeepsOfflineChannelsConservativeWithoutDirectEvidence()
    {
        var service = new LeadChannelDetectionService();
        var lead = new Lead
        {
            Id = 62,
            Name = "Urban Dental",
            Website = "urbandental.co.za",
            Location = "Cape Town",
            Category = "Healthcare"
        };
        var signal = new Signal
        {
            LeadId = 62,
            HasMetaAds = true,
            HasPromo = true,
            WebsiteUpdatedRecently = true,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var results = service.Detect(lead, signal);

        results.Single(x => x.Channel == "tv").Score.Should().BeLessThanOrEqualTo(49);
        results.Single(x => x.Channel == "billboards_ooh").Score.Should().BeLessThanOrEqualTo(49);
        results.Single(x => x.Channel == "radio").Score.Should().BeLessThanOrEqualTo(49);
    }
}

public class LeadActionRecommendationServiceTests
{
    [Fact]
    public void BuildRecommendedActions_CreatesHighPriorityActionsForFreshHighIntentLead()
    {
        var service = new LeadActionRecommendationService();
        var lead = new Lead
        {
            Id = 11,
            Name = "Urban Dental",
            Location = "Cape Town",
            Category = "Healthcare"
        };
        var score = new LeadScoreResult
        {
            LeadId = 11,
            Score = 90,
            IntentLevel = "High"
        };
        var trend = new LeadTrendAnalysisResult
        {
            Summary = "Promotion activity just appeared.",
            CampaignStartedRecently = true,
            ActivityIncreased = true
        };
        var insight = new LeadInsight
        {
            Id = 3,
            LeadId = 11,
            TrendSummary = trend.Summary,
            ScoreSnapshot = score.Score,
            IntentLevelSnapshot = score.IntentLevel,
            Text = "High intent"
        };

        var actions = service.BuildRecommendedActions(lead, score, trend, insight);

        actions.Should().ContainSingle(x => x.ActionType == "outreach" && x.Priority == "high");
        actions.Should().ContainSingle(x => x.ActionType == "campaign_suggestion" && x.Priority == "high");
        actions.Should().NotContain(x => x.ActionType == "monitor");
    }

    [Fact]
    public void BuildRecommendedActions_CreatesMonitorActionForRisingNonLaunchActivity()
    {
        var service = new LeadActionRecommendationService();

        var actions = service.BuildRecommendedActions(
            new Lead { Id = 12, Name = "Fit Lab", Location = "Johannesburg", Category = "Fitness" },
            new LeadScoreResult { LeadId = 12, Score = 55, IntentLevel = "Medium" },
            new LeadTrendAnalysisResult
            {
                Summary = "Website freshness improved recently.",
                CampaignStartedRecently = false,
                ActivityIncreased = true
            },
            new LeadInsight
            {
                Id = 4,
                LeadId = 12,
                TrendSummary = "Website freshness improved recently.",
                ScoreSnapshot = 55,
                IntentLevelSnapshot = "Medium",
                Text = "Moderate intent"
            });

        actions.Should().ContainSingle(x => x.ActionType == "monitor" && x.Priority == "medium");
    }
}

sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}

static class LeadIntelligenceTestHelpers
{
    public static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    public static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "advertified-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

sealed class StubLeadSourceImportService : ILeadSourceImportService
{
    private readonly LeadSourceIngestionResult _result;

    public StubLeadSourceImportService(LeadSourceIngestionResult result)
    {
        _result = result;
    }

    public List<(string CsvText, string DefaultSource, string ImportProfile)> Calls { get; } = new();

    public Task<LeadSourceIngestionResult> ImportCsvAsync(string csvText, string defaultSource, string importProfile, CancellationToken cancellationToken)
    {
        Calls.Add((csvText, defaultSource, importProfile));
        return Task.FromResult(_result);
    }
}

sealed class StubLeadIntelligenceOrchestrator : ILeadIntelligenceOrchestrator
{
    public List<int> ProcessedLeadIds { get; } = new();

    public Task<LeadIntelligenceRunResult> RunLeadAsync(int leadId, CancellationToken cancellationToken)
    {
        ProcessedLeadIds.Add(leadId);
        return Task.FromResult(new LeadIntelligenceRunResult());
    }

    public Task<int> RunAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }
}

sealed class LeadIntelligenceStubWebHostEnvironment : IWebHostEnvironment
{
    public LeadIntelligenceStubWebHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        WebRootPath = contentRootPath;
        ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        WebRootFileProvider = new PhysicalFileProvider(contentRootPath);
    }

    public string ApplicationName { get; set; } = "Advertified.App.Tests";
    public IFileProvider WebRootFileProvider { get; set; }
    public string WebRootPath { get; set; }
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
}
