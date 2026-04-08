using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Advertified.App.Contracts.Admin;
using Advertified.App.Contracts.Agent;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Leads;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
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
    public async Task CollectAsync_DoesNotTreatResponseDateHeaderAsWebsiteFreshness()
    {
        var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>hello</body></html>")
            };
            response.Headers.Date = DateTimeOffset.UtcNow;
            return response;
        }));

        var provider = new WebsiteSignalProvider(client);

        var result = await provider.CollectAsync("https://example.com", CancellationToken.None);

        result.WebsiteUpdatedRecently.Should().BeFalse();
    }

    [Fact]
    public async Task CollectAsync_DoesNotMarkMetaAdsForPlainInstagramLinks()
    {
        var html = "<html><body><a href=\"https://instagram.com/brand\">Instagram</a></body></html>";
        var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) }));
        var provider = new WebsiteSignalProvider(client);

        var result = await provider.CollectAsync("https://example.com", CancellationToken.None);

        result.HasMetaAds.Should().BeFalse();
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

        var service = new LeadScoreService(
            db,
            Options.Create(new LeadScoringOptions
            {
                BaseScore = 0,
                ActivityWeights = new LeadActivityScoringWeights
                {
                    PromoActive = 15,
                    MetaStrong = 20,
                    WebsiteActive = 10,
                    MultiChannelPresence = 5
                },
                OpportunityWeights = new LeadOpportunityScoringWeights
                {
                    DigitalStrongButSearchWeak = 15,
                    DigitalStrongButOohWeak = 15,
                    PromoHeavyButBrandPresenceWeak = 10,
                    SingleChannelDependency = 10
                },
                SignalThresholds = new LeadScoringSignalThresholds
                {
                    StrongChannelMin = 60,
                    WeakChannelMax = 39,
                    ActiveChannelMin = 40
                },
                Thresholds = new LeadIntentThresholds
                {
                    LowMax = 40,
                    MediumMax = 70
                }
            }),
            new LeadChannelDetectionService());

        var result = await service.ScoreAsync(7, CancellationToken.None);

        result.Score.Should().Be(45);
        result.IntentLevel.Should().Be("Medium");
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
    public void Detect_ScoresSocialAsDetectedWhenDirectMetaEvidenceIsPresent()
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

        var evidence = new[]
        {
            new LeadSignalEvidence
            {
                LeadId = 61,
                SignalId = 1,
                Channel = "social",
                SignalType = "meta_ad_library_active_ads",
                Source = "meta_ad_library",
                Confidence = "detected",
                Weight = 40,
                ReliabilityMultiplier = 1.0m,
                FreshnessMultiplier = 1.0m,
                EffectiveWeight = 40m,
                IsPositive = true,
                Value = "Direct active ad evidence."
            }
        };

        var results = service.Detect(lead, signal, evidence);
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

public class RecommendationOpportunityContextParserTests
{
    [Fact]
    public void Parse_ExtractsOpportunityContextAndLeavesCampaignNotes()
    {
        var rawNotes = @"Why you are receiving this:
- You are missing high-intent search traffic.
- You have limited awareness coverage across broad-reach channels.

Lead intelligence summary:
This business appears active on social but weak in broader awareness channels.

Expected impact: more inbound leads, stronger high-intent capture, and clearer demand conversion paths.

Keep the campaign family-safe and visible near commuter routes.";

        var result = RecommendationOpportunityContextParser.Parse(rawNotes);

        result.Context.Should().NotBeNull();
        result.Context!.DetectedGaps.Should().ContainInOrder(
            "You are missing high-intent search traffic.",
            "You have limited awareness coverage across broad-reach channels.");
        result.Context.LeadInsightSummary.Should().Be("This business appears active on social but weak in broader awareness channels.");
        result.Context.ExpectedOutcome.Should().Be("Expected impact: more inbound leads, stronger high-intent capture, and clearer demand conversion paths.");
        result.CampaignNotes.Should().Be("Keep the campaign family-safe and visible near commuter routes.");
    }

    [Fact]
    public void Parse_ReturnsCampaignNotesWhenNoOpportunitySectionsExist()
    {
        var rawNotes = "Keep the campaign family-safe and visible near commuter routes.";

        var result = RecommendationOpportunityContextParser.Parse(rawNotes);

        result.Context.Should().BeNull();
        result.CampaignNotes.Should().Be(rawNotes);
    }
}

public class CampaignRecommendationServiceAuditTests
{
    [Fact]
    public async Task GenerateAndSaveAsync_RecoversProposalBWhenTierIsUnderfilled()
    {
        await using var db = LeadIntelligenceTestHelpers.CreateDbContext();
        var packageBandId = Guid.NewGuid();
        var packageOrderId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        db.PackageBands.Add(new Advertified.App.Data.Entities.PackageBand
        {
            Id = packageBandId,
            Code = "scale",
            Name = "Scale",
            MinBudget = 20_000m,
            MaxBudget = 100_000m,
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        db.PackageOrders.Add(new Advertified.App.Data.Entities.PackageOrder
        {
            Id = packageOrderId,
            PackageBandId = packageBandId,
            Amount = 60_000m,
            SelectedBudget = 60_000m,
            Currency = "ZAR",
            PaymentStatus = "paid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Campaigns.Add(new Advertified.App.Data.Entities.Campaign
        {
            Id = campaignId,
            PackageBandId = packageBandId,
            PackageOrderId = packageOrderId,
            Status = "paid",
            PlanningMode = "ai_assisted",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.CampaignBriefs.Add(new Advertified.App.Data.Entities.CampaignBrief
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            Objective = "Awareness",
            GeographyScope = "local",
            CitiesJson = JsonSerializer.Serialize(new[] { "Johannesburg" }),
            PreferredMediaTypesJson = JsonSerializer.Serialize(new[] { "ooh", "radio", "digital" }),
            TargetLanguagesJson = JsonSerializer.Serialize(new[] { "English" }),
            MaxMediaItems = 3,
            OpenToUpsell = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var policySnapshotProvider = new PlanningPolicySnapshotProvider(new PlanningPolicyOptions());
        var service = new CampaignRecommendationService(
            db,
            new TierRecoveryMediaPlanningEngineStub(),
            new StubRecommendationAuditCampaignReasoningService(),
            policySnapshotProvider,
            new PlanningPolicyService(policySnapshotProvider));

        var recommendationId = await service.GenerateAndSaveAsync(campaignId, null, CancellationToken.None);

        recommendationId.Should().NotBe(Guid.Empty);
        db.CampaignRecommendations.Should().HaveCount(3);

        var proposalB = await db.CampaignRecommendations
            .AsNoTracking()
            .SingleAsync(item => item.RecommendationType.EndsWith(":ooh_focus"));

        proposalB.TotalCost.Should().BeGreaterThanOrEqualTo(46_666.67m);
        proposalB.TotalCost.Should().BeLessThanOrEqualTo(73_333.33m);
        proposalB.Rationale.Should().Contain("tier_recovery_used");
        proposalB.Rationale.Should().Contain("tier_recovery_relaxed_max_media_items");
    }

    [Fact]
    public async Task GenerateAndSaveAsync_PersistsRecommendationRunAudit()
    {
        await using var db = LeadIntelligenceTestHelpers.CreateDbContext();
        var packageBandId = Guid.NewGuid();
        var packageOrderId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        db.PackageBands.Add(new Advertified.App.Data.Entities.PackageBand
        {
            Id = packageBandId,
            Code = "scale",
            Name = "Scale",
            MinBudget = 10_000m,
            MaxBudget = 60_000m,
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        db.PackageOrders.Add(new Advertified.App.Data.Entities.PackageOrder
        {
            Id = packageOrderId,
            PackageBandId = packageBandId,
            Amount = 30_000m,
            SelectedBudget = 30_000m,
            Currency = "ZAR",
            PaymentStatus = "paid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Campaigns.Add(new Advertified.App.Data.Entities.Campaign
        {
            Id = campaignId,
            PackageBandId = packageBandId,
            PackageOrderId = packageOrderId,
            Status = "paid",
            PlanningMode = "ai_assisted",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.CampaignBriefs.Add(new Advertified.App.Data.Entities.CampaignBrief
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            Objective = "Awareness",
            GeographyScope = "local",
            CitiesJson = JsonSerializer.Serialize(new[] { "Johannesburg" }),
            PreferredMediaTypesJson = JsonSerializer.Serialize(new[] { "ooh" }),
            TargetLanguagesJson = JsonSerializer.Serialize(new[] { "English" }),
            OpenToUpsell = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.InventoryImportBatches.Add(new InventoryImportBatch
        {
            Id = Guid.NewGuid(),
            ChannelFamily = "broadcast",
            SourceType = "json",
            SourceIdentifier = "seed.json",
            SourceChecksum = "abc123",
            RecordCount = 27,
            Status = "active",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ActivatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var policyOptions = new PlanningPolicyOptions
        {
            Scale = new PackagePlanningPolicy
            {
                BudgetFloor = 25_000m,
                MinimumNationalRadioCandidates = 1,
                RequireNationalCapableRadio = true,
                RequirePremiumNationalRadio = false,
                NationalRadioBonus = 10,
                NonNationalRadioPenalty = 8,
                RegionalRadioPenalty = 6
            },
            Dominance = new PackagePlanningPolicy
            {
                BudgetFloor = 100_000m,
                MinimumNationalRadioCandidates = 2,
                RequireNationalCapableRadio = true,
                RequirePremiumNationalRadio = true,
                NationalRadioBonus = 12,
                NonNationalRadioPenalty = 10,
                RegionalRadioPenalty = 8
            }
        };
        var policySnapshotProvider = new PlanningPolicySnapshotProvider(policyOptions);
        var service = new CampaignRecommendationService(
            db,
            new StubRecommendationAuditMediaPlanningEngine(),
            new StubRecommendationAuditCampaignReasoningService(),
            policySnapshotProvider,
            new PlanningPolicyService(policySnapshotProvider));

        var recommendationId = await service.GenerateAndSaveAsync(
            campaignId,
            new GenerateRecommendationRequest { TargetOohShare = 100 },
            CancellationToken.None);

        recommendationId.Should().NotBe(Guid.Empty);
        db.CampaignRecommendations.Should().ContainSingle();
        db.RecommendationRunAudits.Should().ContainSingle();

        var recommendation = await db.CampaignRecommendations.SingleAsync();
        var audit = await db.RecommendationRunAudits.SingleAsync();

        audit.RecommendationId.Should().Be(recommendation.Id);
        audit.CampaignId.Should().Be(campaignId);
        audit.ManualReviewRequired.Should().BeTrue();
        audit.BudgetUtilizationRatio.Should().Be(0.4m);
        audit.RequestSnapshotJson.Should().NotBeNullOrWhiteSpace();
        audit.PolicySnapshotJson.Should().NotBeNullOrWhiteSpace();
        audit.InventorySnapshotJson.Should().NotBeNullOrWhiteSpace();
        audit.InventoryBatchRefsJson.Should().NotBeNullOrWhiteSpace();
        audit.CandidateCountsJson.Should().NotBeNullOrWhiteSpace();
        audit.RejectedCandidatesJson.Should().NotBeNullOrWhiteSpace();
        audit.SelectedItemsJson.Should().NotBeNullOrWhiteSpace();
        audit.FallbackFlagsJson.Should().NotBeNullOrWhiteSpace();
        recommendation.RequestSnapshotJson.Should().NotBeNullOrWhiteSpace();
        recommendation.PolicySnapshotJson.Should().NotBeNullOrWhiteSpace();
        recommendation.InventorySnapshotJson.Should().NotBeNullOrWhiteSpace();
        recommendation.InventoryBatchRefsJson.Should().NotBeNullOrWhiteSpace();

        using var requestSnapshot = JsonDocument.Parse(audit.RequestSnapshotJson!);
        requestSnapshot.RootElement.GetProperty("selectedBudget").GetDecimal().Should().Be(30_000m);

        using var candidateCounts = JsonDocument.Parse(audit.CandidateCountsJson!);
        candidateCounts.RootElement.EnumerateArray()
            .Any(item => item.GetProperty("stage").GetString() == "loaded"
                && item.GetProperty("mediaType").GetString() == "ooh"
                && item.GetProperty("count").GetInt32() == 12)
            .Should()
            .BeTrue();

        using var rejectedCandidates = JsonDocument.Parse(audit.RejectedCandidatesJson!);
        rejectedCandidates.RootElement.EnumerateArray()
            .Any(item => item.GetProperty("reason").GetString() == "geography_mismatch")
            .Should()
            .BeTrue();

        using var selectedItems = JsonDocument.Parse(audit.SelectedItemsJson!);
        selectedItems.RootElement.EnumerateArray()
            .Any(item => item.GetProperty("displayName").GetString() == "Johannesburg North Mega Board")
            .Should()
            .BeTrue();

        using var batchRefs = JsonDocument.Parse(audit.InventoryBatchRefsJson!);
        batchRefs.RootElement.EnumerateArray()
            .Any(item => item.GetProperty("channelFamily").GetString() == "broadcast"
                && item.GetProperty("sourceIdentifier").GetString() == "seed.json")
            .Should()
            .BeTrue();
    }
}

public class PlanningPolicyServiceTests
{
    [Fact]
    public void BuildPolicyContext_CentralizesRequestedMixAndRequiredChannels()
    {
        var snapshotProvider = new PlanningPolicySnapshotProvider(new PlanningPolicyOptions
        {
            Scale = new PackagePlanningPolicy
            {
                BudgetFloor = 25_000m,
                MinimumNationalRadioCandidates = 1,
                RequireNationalCapableRadio = true,
                RequirePremiumNationalRadio = false,
                NationalRadioBonus = 10,
                NonNationalRadioPenalty = 8,
                RegionalRadioPenalty = 6
            },
            Dominance = new PackagePlanningPolicy
            {
                BudgetFloor = 100_000m,
                MinimumNationalRadioCandidates = 2,
                RequireNationalCapableRadio = true,
                RequirePremiumNationalRadio = true,
                NationalRadioBonus = 12,
                NonNationalRadioPenalty = 10,
                RegionalRadioPenalty = 8
            }
        });
        var service = new PlanningPolicyService(snapshotProvider);

        var context = service.BuildPolicyContext(new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 150_000m,
            PreferredMediaTypes = new List<string> { "radio", "tv" },
            TargetRadioShare = 40,
            TargetOohShare = 30,
            TargetDigitalShare = 20
        });

        context.PackagePolicyCode.Should().Be("dominance");
        context.RequestedMixLabel.Should().Be("Radio 40% | Billboards and Digital Screens 30% | Digital 20%");
        context.RequestedChannelShares.Should().ContainSingle(x => x.Channel == "tv" && x.Share == 10);
        context.RequiredChannels.Should().Contain(new[] { "radio", "ooh", "digital", "tv" });
    }
}

public class CampaignBriefInterpretationServiceTests
{
    [Fact]
    public async Task InterpretAsync_UsesCanonicalProvinceCodesInHeuristicFallback()
    {
        var service = new CampaignBriefInterpretationService(
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError))),
            Options.Create(new OpenAIOptions
            {
                Enabled = false
            }),
            NullLogger<CampaignBriefInterpretationService>.Instance,
            new StubBroadcastMasterDataService());

        var result = await service.InterpretAsync(
            new InterpretCampaignBriefRequest
            {
                SelectedBudget = 250000m,
                Brief = "Run a Durban radio push with local awareness focus"
            },
            CancellationToken.None);

        result.Geography.Should().Be("kwazulu_natal");
    }
}

public class CampaignGeographyNormalizerTests
{
    [Fact]
    public void Normalize_ConvertsInvalidProvincialValueToLocalAreaAndCity()
    {
        var result = CampaignGeographyNormalizer.Normalize(
            "provincial",
            new[] { "Hyde Park" },
            null,
            null,
            null);

        result.Scope.Should().Be("local");
        result.Areas.Should().ContainSingle().Which.Should().Be("Hyde Park");
        result.Cities.Should().Contain("Johannesburg");
        result.Provinces.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_ConvertsCanonicalProvinceLabels()
    {
        var result = CampaignGeographyNormalizer.Normalize(
            "provincial",
            new[] { "gauteng", "KZN" },
            null,
            null,
            null);

        result.Scope.Should().Be("provincial");
        result.Provinces.Should().BeEquivalentTo(new[] { "Gauteng", "KwaZulu-Natal" });
        result.Cities.Should().BeEmpty();
        result.Areas.Should().BeEmpty();
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

sealed class StubRecommendationAuditMediaPlanningEngine : IMediaPlanningEngine
{
    public Task<RecommendationResult> GenerateAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new RecommendationResult
        {
            RecommendedPlan = new List<PlannedItem>
            {
                new PlannedItem
                {
                    SourceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    SourceType = "ooh",
                    DisplayName = "Johannesburg North Mega Board",
                    MediaType = "ooh",
                    UnitCost = 12_000m,
                    Quantity = 1,
                    Score = 87m,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["selectionReasons"] = new[] { "Strong local reach", "Fits the requested OOH mix" },
                        ["policyFlags"] = new[] { "mix_target_matched" },
                        ["confidenceScore"] = "high"
                    }
                }
            },
            FallbackFlags = new List<string> { "inventory_insufficient" },
            ManualReviewRequired = true,
            Rationale = "Selected for strong geography and audience fit.",
            RunTrace = new RecommendationRunTrace
            {
                RequestSnapshot = new CampaignPlanningRequestSnapshot
                {
                    CampaignId = request.CampaignId,
                    SelectedBudget = request.SelectedBudget,
                    Objective = request.Objective,
                    GeographyScope = request.GeographyScope,
                    Cities = request.Cities.ToList(),
                    PreferredMediaTypes = request.PreferredMediaTypes.ToList(),
                    TargetLanguages = request.TargetLanguages.ToList(),
                    TargetOohShare = request.TargetOohShare
                },
                CandidateCounts = new List<RecommendationTraceCount>
                {
                    new() { Stage = "loaded", MediaType = "ooh", Count = 12 },
                    new() { Stage = "loaded", MediaType = "radio", Count = 4 },
                    new() { Stage = "eligible", MediaType = "ooh", Count = 5 },
                    new() { Stage = "eligible", MediaType = "radio", Count = 2 },
                    new() { Stage = "selected", MediaType = "ooh", Count = 1 }
                },
                RejectedCandidates = new List<RecommendationRejectedCandidateTrace>
                {
                    new()
                    {
                        Stage = "eligibility",
                        Reason = "geography_mismatch",
                        SourceId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        DisplayName = "Cape Town Transit Screen",
                        MediaType = "ooh"
                    }
                },
                SelectedItems = new List<RecommendationSelectedItemTrace>
                {
                    new()
                    {
                        SourceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        DisplayName = "Johannesburg North Mega Board",
                        MediaType = "ooh",
                        Score = 87m,
                        TotalCost = 12_000m,
                        SelectionReasons = new[] { "Strong local reach", "Fits the requested OOH mix" },
                        PolicyFlags = new[] { "mix_target_matched" },
                        ConfidenceScore = "high"
                    }
                }
            }
        });
    }
}

sealed class TierRecoveryMediaPlanningEngineStub : IMediaPlanningEngine
{
    public Task<RecommendationResult> GenerateAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var total = ResolveTotal(request);
        return Task.FromResult(new RecommendationResult
        {
            RecommendedPlan = new List<PlannedItem>
            {
                new PlannedItem
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "ooh",
                    DisplayName = "Tier Recovery Test Placement",
                    MediaType = "ooh",
                    UnitCost = total,
                    Quantity = 1,
                    Score = 80m,
                    Metadata = new Dictionary<string, object?>()
                }
            },
            FallbackFlags = new List<string>(),
            ManualReviewRequired = false,
            Rationale = "Generated for tier recovery validation."
        });
    }

    private static decimal ResolveTotal(CampaignPlanningRequest request)
    {
        if (request.TargetOohShare == 60 && request.MaxMediaItems.HasValue)
        {
            return 18_480m;
        }

        if (request.TargetOohShare == 60 && !request.MaxMediaItems.HasValue)
        {
            return 55_000m;
        }

        return request.SelectedBudget * 0.8m;
    }
}

sealed class StubRecommendationAuditCampaignReasoningService : ICampaignReasoningService
{
    public Task<CampaignReasoningResult?> GenerateAsync(
        Advertified.App.Data.Entities.Campaign campaign,
        Advertified.App.Data.Entities.CampaignBrief brief,
        CampaignPlanningRequest planningRequest,
        RecommendationResult recommendationResult,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<CampaignReasoningResult?>(new CampaignReasoningResult
        {
            Summary = "OOH-focused recommendation for local awareness.",
            Rationale = "The selected board aligns strongly with the requested local reach strategy."
        });
    }
}

sealed class StubBroadcastMasterDataService : IBroadcastMasterDataService
{
    public Task<AdminOutletMasterDataResponse> GetOutletMasterDataAsync(CancellationToken cancellationToken)
        => Task.FromResult(new AdminOutletMasterDataResponse());

    public string NormalizeLanguageCode(string? value) => value ?? string.Empty;

    public string NormalizeProvinceCode(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        return normalized switch
        {
            "western_cape" => "western_cape",
            "kwazulu_natal" => "kwazulu_natal",
            "national" => "national",
            _ => "gauteng"
        };
    }

    public string NormalizeCoverageType(string? value) => value ?? string.Empty;
    public string NormalizeCatalogHealth(string? value) => value ?? string.Empty;
    public string NormalizeLanguageForMatching(string? value) => value ?? string.Empty;
    public string NormalizeGeographyForMatching(string? value) => value ?? string.Empty;
}
