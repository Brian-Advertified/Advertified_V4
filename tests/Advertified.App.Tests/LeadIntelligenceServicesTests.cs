using System.Net;
using System.Net.Http.Headers;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
}
