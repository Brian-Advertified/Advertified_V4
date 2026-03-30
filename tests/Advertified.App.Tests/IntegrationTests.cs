using System.Net;
using System.Net.Http.Json;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Agent;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Advertified.App.Tests;

public class HttpWorkflowIntegrationTests
{
    [Fact]
    public async Task CampaignBriefWorkflow_SavesSubmitsAndSelectsPlanningModeOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var user = TestSeed.CreateUser();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(user, selectedBudget: 250000m);

                db.UserAccounts.Add(user);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubTemplatedEmailService>();
                services.AddSingleton<ITemplatedEmailService>(sp => sp.GetRequiredService<StubTemplatedEmailService>());
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var userId = await harness.ExecuteDbAsync(db => db.UserAccounts.Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(userId));

        var saveResponse = await harness.Client.PutAsJsonAsync($"/campaigns/{campaignId}/brief", new
        {
            objective = "launch",
            geographyScope = "regional",
            provinces = new[] { "Gauteng" },
            preferredMediaTypes = new[] { "radio", "ooh" },
            openToUpsell = false,
            specialRequirements = "Retail launch campaign"
        });

        saveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var submitResponse = await harness.Client.PostAsJsonAsync($"/campaigns/{campaignId}/brief/submit", new { });
        submitResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var planningModeResponse = await harness.Client.PostAsJsonAsync($"/campaigns/{campaignId}/planning-mode", new
        {
            planningMode = "hybrid"
        });

        planningModeResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var campaign = await harness.ExecuteDbAsync(db => db.Campaigns.Include(x => x.CampaignBrief).SingleAsync(x => x.Id == campaignId));
        campaign.Status.Should().Be("planning_in_progress");
        campaign.PlanningMode.Should().Be("hybrid");
        campaign.AiUnlocked.Should().BeTrue();
        campaign.CampaignBrief.Should().NotBeNull();
        campaign.CampaignBrief!.Objective.Should().Be("launch");
        campaign.CampaignBrief.SubmittedAt.Should().NotBeNull();

        var emailService = harness.Services.GetRequiredService<StubTemplatedEmailService>();
        emailService.SentEmails.Select(x => x.TemplateName).Should().Contain("brief-submitted");
    }

    [Fact]
    public async Task AgentInterpretBrief_ReturnsStructuredInputsOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var user = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(user, selectedBudget: 500000m);

                db.UserAccounts.AddRange(user, agentUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<ICampaignBriefInterpretationService>(new StubCampaignBriefInterpretationService());
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var agentUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Agent).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(agentUserId));

        var response = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/interpret-brief", new
        {
            brief = "Launch a premium Gauteng retail campaign with radio and billboards.",
            campaignName = "Black Space Launch",
            selectedBudget = 500000m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<InterpretedCampaignBriefResponse>();
        payload.Should().NotBeNull();
        payload!.Objective.Should().Be("launch");
        payload.Geography.Should().Be("gauteng");
        payload.Channels.Should().Contain(new[] { "Radio", "OOH" });
    }

    [Fact]
    public async Task AgentCampaignQueue_AssignsAndUnassignsCampaignOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 180000m, status: "planning_in_progress");
                campaign.AssignedAgentUserId = null;
                campaign.AssignedAt = null;

                db.UserAccounts.AddRange(clientUser, agentUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var agentUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Agent).Select(x => x.Id).SingleAsync());

        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(agentUserId));

        var assignResponse = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/assign", new { });
        assignResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var assignedCampaign = await harness.ExecuteDbAsync(db => db.Campaigns.SingleAsync(x => x.Id == campaignId));
        assignedCampaign.AssignedAgentUserId.Should().Be(agentUserId);
        assignedCampaign.AssignedAt.Should().NotBeNull();

        var inboxAfterAssign = await harness.Client.GetFromJsonAsync<AgentInboxResponse>("/agent/campaigns/inbox");
        inboxAfterAssign.Should().NotBeNull();
        inboxAfterAssign!.AssignedToMeCount.Should().Be(1);
        inboxAfterAssign.UnassignedCount.Should().Be(0);
        inboxAfterAssign.Items.Should().ContainSingle(x => x.Id == campaignId && x.IsAssignedToCurrentUser && !x.IsUnassigned);

        var unassignResponse = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/unassign", new { });
        unassignResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var unassignedCampaign = await harness.ExecuteDbAsync(db => db.Campaigns.SingleAsync(x => x.Id == campaignId));
        unassignedCampaign.AssignedAgentUserId.Should().BeNull();

        var inboxAfterUnassign = await harness.Client.GetFromJsonAsync<AgentInboxResponse>("/agent/campaigns/inbox");
        inboxAfterUnassign.Should().NotBeNull();
        inboxAfterUnassign!.AssignedToMeCount.Should().Be(0);
        inboxAfterUnassign.UnassignedCount.Should().Be(1);
        inboxAfterUnassign.Items.Should().ContainSingle(x => x.Id == campaignId && !x.IsAssignedToCurrentUser && x.IsUnassigned);
    }

    [Fact]
    public async Task RecommendationReviewWorkflow_SendsRequestsChangesAndApprovesOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "review_ready");
                campaign.AssignedAgentUserId = agentUser.Id;
                campaign.AssignedAt = DateTime.UtcNow;
                campaign.Status = "planning_in_progress";

                var recommendation = TestSeed.CreateRecommendation(campaign.Id, status: "draft");

                db.UserAccounts.AddRange(clientUser, agentUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.CampaignRecommendations.Add(recommendation);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubTemplatedEmailService>();
                services.AddSingleton<ITemplatedEmailService>(sp => sp.GetRequiredService<StubTemplatedEmailService>());
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var clientUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Client).Select(x => x.Id).SingleAsync());
        var agentUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Agent).Select(x => x.Id).SingleAsync());

        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(agentUserId));
        var sendResponse = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/send-to-client", new
        {
            message = "Please review the attached media recommendation and let us know if you want any changes."
        });

        sendResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var sentEmailService = harness.Services.GetRequiredService<StubTemplatedEmailService>();
        sentEmailService.SentEmails.Should().ContainSingle();
        sentEmailService.SentEmails[0].TemplateName.Should().Be("recommendation-ready");

        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(clientUserId));

        var requestChangesResponse = await harness.Client.PostAsJsonAsync($"/campaigns/{campaignId}/request-changes", new
        {
            notes = "Please make the mix more retail-focused."
        });

        requestChangesResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var changedCampaign = await harness.ExecuteDbAsync(db => db.Campaigns.Include(x => x.CampaignRecommendations).SingleAsync(x => x.Id == campaignId));
        changedCampaign.Status.Should().Be("planning_in_progress");
        changedCampaign.CampaignRecommendations.Should().Contain(x => x.Status == "draft");
        changedCampaign.CampaignRecommendations.Should().Contain(x => (x.Rationale ?? string.Empty).Contains("Client feedback:"));

        await harness.ExecuteDbAsync(async db =>
        {
            var reviewReadyCampaign = await db.Campaigns.Include(x => x.CampaignRecommendations).SingleAsync(x => x.Id == campaignId);
            reviewReadyCampaign.Status = "review_ready";
            reviewReadyCampaign.CampaignRecommendations
                .OrderByDescending(x => x.CreatedAt)
                .First()
                .Status = "sent_to_client";
            await db.SaveChangesAsync();
            return true;
        });

        var approveResponse = await harness.Client.PostAsJsonAsync($"/campaigns/{campaignId}/approve-recommendation", new { });
        approveResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var approvedCampaign = await harness.ExecuteDbAsync(db => db.Campaigns.Include(x => x.CampaignRecommendations).SingleAsync(x => x.Id == campaignId));
        approvedCampaign.Status.Should().Be("approved");
        approvedCampaign.CampaignRecommendations.Should().Contain(x => x.Status == "approved");
        sentEmailService.SentEmails.Select(x => x.TemplateName).Should().Contain(new[]
        {
            "recommendation-ready",
            "recommendation-approved",
            "activation-in-progress"
        });
    }

    [Fact]
    public async Task AgentGenerateRecommendation_SendsPreparingEmailOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "planning_in_progress");
                campaign.AiUnlocked = true;
                campaign.AssignedAgentUserId = agentUser.Id;
                campaign.AssignedAt = DateTime.UtcNow;
                campaign.PlanningMode = "hybrid";
                campaign.CampaignBrief = new Advertified.App.Data.Entities.CampaignBrief
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    Objective = "launch",
                    GeographyScope = "regional",
                    ProvincesJson = "[\"Gauteng\"]",
                    PreferredMediaTypesJson = "[\"radio\",\"ooh\"]",
                    OpenToUpsell = false,
                    SubmittedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.UserAccounts.AddRange(clientUser, agentUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubTemplatedEmailService>();
                services.AddSingleton<ITemplatedEmailService>(sp => sp.GetRequiredService<StubTemplatedEmailService>());
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var agentUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Agent).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(agentUserId));

        var response = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/generate-recommendation", new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var emailService = harness.Services.GetRequiredService<StubTemplatedEmailService>();
        emailService.SentEmails.Select(x => x.TemplateName).Should().Contain("agent-working");
        emailService.SentEmails.Select(x => x.TemplateName).Should().NotContain("recommendation-preparing");

        var savedCampaign = await harness.ExecuteDbAsync(db => db.Campaigns.Include(x => x.CampaignRecommendations).SingleAsync(x => x.Id == campaignId));
        savedCampaign.CampaignRecommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OperationsCanMarkCampaignLaunchedAfterCreativeApproval()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "creative_approved");
                campaign.AssignedAgentUserId = agentUser.Id;
                campaign.AssignedAt = DateTime.UtcNow;

                db.UserAccounts.AddRange(clientUser, agentUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubTemplatedEmailService>();
                services.AddSingleton<ITemplatedEmailService>(sp => sp.GetRequiredService<StubTemplatedEmailService>());
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var agentUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Agent).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(agentUserId));

        var response = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/mark-launched", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var launchedCampaign = await harness.ExecuteDbAsync(db => db.Campaigns.SingleAsync(x => x.Id == campaignId));
        launchedCampaign.Status.Should().Be("launched");

        var emailService = harness.Services.GetRequiredService<StubTemplatedEmailService>();
        emailService.SentEmails.Select(x => x.TemplateName).Should().Contain("campaign-live");
    }

    [Fact]
    public async Task AgentCanPersistSupplierExecutionAndDeliveryReport()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "launched");
                campaign.AssignedAgentUserId = agentUser.Id;
                campaign.AssignedAt = DateTime.UtcNow;

                db.UserAccounts.AddRange(clientUser, agentUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubTemplatedEmailService>();
                services.AddSingleton<ITemplatedEmailService>(sp => sp.GetRequiredService<StubTemplatedEmailService>());
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var agentUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Agent).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(agentUserId));

        var bookingResponse = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/supplier-bookings", new
        {
            supplierOrStation = "Metro FM",
            channel = "radio",
            bookingStatus = "booked",
            committedAmount = 45000m,
            liveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            liveTo = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(13)),
            notes = "Confirmed for launch burst"
        });

        bookingResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var bookingId = await harness.ExecuteDbAsync(db => db.CampaignSupplierBookings.Select(x => x.Id).SingleAsync());

        var reportResponse = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/delivery-reports", new
        {
            supplierBookingId = bookingId,
            reportType = "delivery_update",
            headline = "Week one delivery confirmed",
            summary = "Stations have started airing the booked spots.",
            impressions = 120000L,
            playsOrSpots = 84,
            spendDelivered = 22500m
        });

        reportResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var campaign = await harness.ExecuteDbAsync(db => db.Campaigns
            .Include(x => x.CampaignSupplierBookings)
            .Include(x => x.CampaignDeliveryReports)
            .SingleAsync(x => x.Id == campaignId));

        campaign.CampaignSupplierBookings.Should().ContainSingle(x => x.SupplierOrStation == "Metro FM" && x.BookingStatus == "booked");
        campaign.CampaignDeliveryReports.Should().ContainSingle(x => x.Headline == "Week one delivery confirmed" && x.PlaysOrSpots == 84);

        var emailService = harness.Services.GetRequiredService<StubTemplatedEmailService>();
        emailService.SentEmails.Select(x => x.TemplateName).Should().Contain(new[]
        {
            "campaign-booking-confirmed",
            "campaign-report-available"
        });
    }

    [Fact]
    public async Task AdminCanProcessRefundBeforeWorkStarts()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var adminUser = TestSeed.CreateAdmin();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 38000m, status: "paid");
                order.Amount = 41800m;

                db.UserAccounts.AddRange(clientUser, adminUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var adminUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Admin).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var response = await harness.Client.PostAsJsonAsync($"/admin/campaign-operations/{campaignId}/refund", new
        {
            gatewayFeeRetainedAmount = 800m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await harness.ExecuteDbAsync(db => db.PackageOrders.SingleAsync());
        order.RefundedAmount.Should().Be(41000m);
        order.GatewayFeeRetainedAmount.Should().Be(800m);
        order.RefundStatus.Should().Be("refunded");
    }

    [Fact]
    public async Task AdminCanPauseAndResumeCampaignAndCarryPausedDays()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var adminUser = TestSeed.CreateAdmin();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "launched");
                campaign.CampaignBrief = new CampaignBrief
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    Objective = "launch",
                    GeographyScope = "regional",
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
                    EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.UserAccounts.AddRange(clientUser, adminUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var adminUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Admin).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var pauseResponse = await harness.Client.PostAsJsonAsync($"/admin/campaign-operations/{campaignId}/pause", new
        {
            reason = "Waiting for client stock confirmation"
        });

        pauseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await harness.ExecuteDbAsync(async db =>
        {
            var campaign = await db.Campaigns.SingleAsync(x => x.Id == campaignId);
            campaign.PausedAt = DateTime.UtcNow.AddDays(-3);
            await db.SaveChangesAsync();
            return true;
        });

        var resumeResponse = await harness.Client.PostAsJsonAsync($"/admin/campaign-operations/{campaignId}/unpause", new
        {
            reason = "Stock confirmed and campaign resumed"
        });

        resumeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var campaignAfterResume = await harness.ExecuteDbAsync(db => db.Campaigns.Include(x => x.CampaignBrief).SingleAsync(x => x.Id == campaignId));
        campaignAfterResume.PausedAt.Should().BeNull();
        campaignAfterResume.TotalPausedDays.Should().Be(3);
        campaignAfterResume.PauseReason.Should().Be("Stock confirmed and campaign resumed");
    }
}

public class ResendEmailServiceFallbackTests
{
    [Fact]
    public async Task SendAsync_ArchivesEmailLocallyWhenApiKeyIsMissing()
    {
        var archiveRoot = Path.Combine(Path.GetTempPath(), $"advertified-email-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(archiveRoot);

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

            await using var db = new AppDbContext(options);
            db.EmailTemplates.Add(new EmailTemplate
            {
                Id = Guid.NewGuid(),
                TemplateName = "test-template",
                SubjectTemplate = "Hello {{Name}}",
                BodyHtmlTemplate = "<p>Hello {{Name}}</p>",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var service = new ResendEmailService(
                new HttpClient { BaseAddress = new Uri("https://api.resend.com/") },
                db,
                Options.Create(new ResendOptions
                {
                    ApiKey = string.Empty,
                    LocalArchiveDirectory = "outbox",
                    SenderAddresses = new Dictionary<string, string>
                    {
                        ["noreply"] = "Advertified <noreply@advertified.com>"
                    }
                }),
                new StubWebHostEnvironment(archiveRoot),
                NullLogger<ResendEmailService>.Instance);

            await service.SendAsync(
                "test-template",
                "client@example.com",
                "noreply",
                new Dictionary<string, string?> { ["Name"] = "Brian" },
                null,
                CancellationToken.None);

            var outboxDirectory = Path.Combine(archiveRoot, "outbox");
            Directory.Exists(outboxDirectory).Should().BeTrue();
            var archivedFolder = Directory.GetDirectories(outboxDirectory).Should().ContainSingle().Subject;
            File.Exists(Path.Combine(archivedFolder, "message.html")).Should().BeTrue();
            File.ReadAllText(Path.Combine(archivedFolder, "message.html")).Should().Contain("Hello Brian");
            File.ReadAllText(Path.Combine(archivedFolder, "metadata.txt")).Should().Contain("Template: test-template");
        }
        finally
        {
            if (Directory.Exists(archiveRoot))
            {
                Directory.Delete(archiveRoot, recursive: true);
            }
        }
    }
}

internal sealed class TestApiHarness : IAsyncDisposable
{
    private readonly IHost _host;

    private TestApiHarness(IHost host, HttpClient client)
    {
        _host = host;
        Client = client;
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => _host.Services;

    public static async Task<TestApiHarness> CreateAsync(Action<AppDbContext> seed, Action<IServiceCollection>? configureServices = null)
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddControllers().AddApplicationPart(typeof(Advertified.App.Controllers.CampaignsController).Assembly);
        builder.Services.Configure<FrontendOptions>(options => options.BaseUrl = "http://localhost:5173");
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        builder.Services.AddScoped<ISessionTokenService, SessionTokenService>();
        builder.Services.AddScoped<IPasswordHashingService, PasswordHashingService>();
        builder.Services.AddScoped<IChangeAuditService, ChangeAuditService>();
        builder.Services.AddScoped<ICampaignAccessService, CampaignAccessService>();
        builder.Services.AddScoped<IAgentAreaRoutingService, StubAgentAreaRoutingService>();
        builder.Services.AddScoped<ICampaignBriefService, CampaignBriefService>();
        builder.Services.AddScoped<CampaignPlanningRequestValidator>();
        builder.Services.AddScoped<SaveCampaignBriefRequestValidator>();
        builder.Services.AddScoped<IEmailVerificationService, StubEmailVerificationService>();
        builder.Services.AddScoped<IInvoiceService, StubInvoiceService>();
        builder.Services.AddScoped<IRecommendationDocumentService, StubRecommendationDocumentService>();
        builder.Services.AddScoped<IPublicAssetStorage, StubPublicAssetStorage>();
        builder.Services.AddScoped<IMediaPlanningEngine, StubMediaPlanningEngine>();
        builder.Services.AddScoped<ICampaignReasoningService, StubCampaignReasoningService>();
        builder.Services.AddScoped<ICampaignRecommendationService, CampaignRecommendationService>();
        builder.Services.AddScoped<ICampaignBriefInterpretationService, StubCampaignBriefInterpretationService>();
        builder.Services.AddScoped<ITemplatedEmailService, StubTemplatedEmailService>();

        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.MapControllers();

        await app.StartAsync();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            seed(db);
        }

        return new TestApiHarness(app, app.GetTestClient());
    }

    public async Task<T> ExecuteDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    public async Task<T> ExecuteDbAsync<T>(Func<AppDbContext, T> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return action(db);
    }

    public async Task<string> CreateSessionTokenAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ISessionTokenService>();
        var user = await db.UserAccounts.FirstAsync(x => x.Id == userId);
        return tokenService.CreateToken(user);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }
}

internal static class TestSeed
{
    public static UserAccount CreateUser()
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        return new UserAccount
        {
            Id = userId,
            FullName = "Brian Rapula",
            Email = "brian@example.com",
            Phone = "0821234567",
            PasswordHash = "hash",
            IsSaCitizen = true,
            EmailVerified = true,
            PhoneVerified = true,
            Role = UserRole.Client,
            AccountStatus = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            BusinessProfile = new BusinessProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BusinessName = "Black Space PSG (Pty) Ltd",
                BusinessType = "pty_ltd",
                RegistrationNumber = $"2026/{Random.Shared.Next(100000, 999999)}/07",
                Industry = "Health",
                AnnualRevenueBand = "r1m_r5m",
                StreetAddress = "1 Main Road",
                City = "Johannesburg",
                Province = "Gauteng",
                CreatedAt = now,
                UpdatedAt = now
            }
        };
    }

    public static UserAccount CreateAgent()
    {
        var now = DateTime.UtcNow;
        return new UserAccount
        {
            Id = Guid.NewGuid(),
            FullName = "Advertified Test Agent",
            Email = "agent@example.com",
            Phone = "0820000000",
            PasswordHash = "hash",
            IsSaCitizen = true,
            EmailVerified = true,
            PhoneVerified = true,
            Role = UserRole.Agent,
            AccountStatus = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static UserAccount CreateAdmin()
    {
        var now = DateTime.UtcNow;
        return new UserAccount
        {
            Id = Guid.NewGuid(),
            FullName = "Advertified Test Admin",
            Email = "admin@example.com",
            Phone = "0821111111",
            PasswordHash = "hash",
            IsSaCitizen = true,
            EmailVerified = true,
            PhoneVerified = true,
            Role = UserRole.Admin,
            AccountStatus = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static (PackageBand band, PackageOrder order, Campaign campaign) CreateCampaignGraph(UserAccount user, decimal selectedBudget, string status = "paid")
    {
        var now = DateTime.UtcNow;
        var band = new PackageBand
        {
            Id = Guid.NewGuid(),
            Code = selectedBudget >= 500000m ? "dominance" : "scale",
            Name = selectedBudget >= 500000m ? "Dominance" : "Scale",
            MinBudget = selectedBudget >= 500000m ? 500000m : 150000m,
            MaxBudget = selectedBudget >= 500000m ? 5000000m : 500000m,
            SortOrder = selectedBudget >= 500000m ? 4 : 3,
            IsActive = true,
            CreatedAt = now
        };

        var order = new PackageOrder
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PackageBandId = band.Id,
            Amount = selectedBudget,
            SelectedBudget = selectedBudget,
            Currency = "ZAR",
            PaymentProvider = "vodapay",
            PaymentStatus = "paid",
            RefundStatus = "none",
            CreatedAt = now,
            UpdatedAt = now,
            PurchasedAt = now
        };

        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PackageOrderId = order.Id,
            PackageBandId = band.Id,
            CampaignName = $"{band.Name} campaign",
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
            AiUnlocked = status != "paid",
            AgentAssistanceRequested = false
        };

        return (band, order, campaign);
    }

    public static CampaignRecommendation CreateRecommendation(Guid campaignId, string status)
    {
        var now = DateTime.UtcNow;
        return new CampaignRecommendation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            RecommendationType = "hybrid",
            GeneratedBy = "system",
            Status = status,
            TotalCost = 250000m,
            Summary = "Recommended mix",
            Rationale = "Plan built within budget.",
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}

internal sealed class StubCampaignBriefInterpretationService : ICampaignBriefInterpretationService
{
    public Task<InterpretedCampaignBriefResponse> InterpretAsync(InterpretCampaignBriefRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new InterpretedCampaignBriefResponse
        {
            Objective = "launch",
            Audience = "retail",
            Scope = "regional",
            Geography = "gauteng",
            Tone = "premium",
            CampaignName = request.CampaignName ?? "Campaign recommendation",
            Channels = new[] { "Radio", "OOH" },
            Summary = "The brief points to a premium Gauteng retail launch across radio and outdoor."
        });
    }
}

internal sealed class StubAgentAreaRoutingService : IAgentAreaRoutingService
{
    public Task TryAssignCampaignAsync(Guid campaignId, string trigger, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class StubRecommendationDocumentService : IRecommendationDocumentService
{
    public Task<byte[]> GetCampaignPdfBytesAsync(Guid campaignId, CancellationToken cancellationToken)
        => Task.FromResult(Array.Empty<byte>());
}

internal sealed class StubTemplatedEmailService : ITemplatedEmailService
{
    public List<(string TemplateName, string RecipientEmail, IReadOnlyDictionary<string, string?> Tokens)> SentEmails { get; } = new();

    public Task SendAsync(
        string templateName,
        string recipientEmail,
        string senderKey,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlyCollection<EmailAttachment>? attachments,
        CancellationToken cancellationToken)
    {
        SentEmails.Add((templateName, recipientEmail, tokens));
        return Task.CompletedTask;
    }
}

internal sealed class StubMediaPlanningEngine : IMediaPlanningEngine
{
    public Task<Advertified.App.Domain.Campaigns.RecommendationResult> GenerateAsync(Advertified.App.Contracts.Campaigns.CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Advertified.App.Domain.Campaigns.RecommendationResult
        {
            RecommendedPlan = new List<Advertified.App.Domain.Campaigns.PlannedItem>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Metro FM Breakfast",
                    MediaType = "Radio",
                    UnitCost = 25000m,
                    Quantity = 1,
                    Score = 91m,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["selectionReasons"] = new[] { "Strong geography match", "Matches requested channel mix" },
                        ["confidenceScore"] = 0.86m,
                        ["province"] = "Gauteng",
                        ["language"] = "English",
                        ["daypart"] = "Breakfast",
                        ["durationSeconds"] = 30
                    }
                }
            },
            Rationale = "Plan built within budget."
        });
    }
}

internal sealed class StubCampaignReasoningService : ICampaignReasoningService
{
    public Task<Advertified.App.Domain.Campaigns.CampaignReasoningResult?> GenerateAsync(
        Campaign campaign,
        CampaignBrief brief,
        Advertified.App.Contracts.Campaigns.CampaignPlanningRequest request,
        Advertified.App.Domain.Campaigns.RecommendationResult result,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<Advertified.App.Domain.Campaigns.CampaignReasoningResult?>(null);
    }
}

internal sealed class StubEmailVerificationService : IEmailVerificationService
{
    public Task QueueActivationEmailAsync(UserAccount user, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<UserAccount> VerifyAsync(string token, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task ResendActivationAsync(string email, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class StubInvoiceService : IInvoiceService
{
    public Task<Invoice> EnsureInvoiceAsync(
        PackageOrder order,
        PackageBand band,
        UserAccount user,
        BusinessProfile? businessProfile,
        string invoiceType,
        string status,
        DateTime? dueAtUtc,
        DateTime? paidAtUtc,
        string? paymentReference,
        bool sendInvoiceEmail,
        CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<byte[]> GetPdfBytesAsync(Guid invoiceId, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}

internal sealed class StubPublicAssetStorage : IPublicAssetStorage
{
    public Task<string> SaveAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken)
        => Task.FromResult(objectKey);

    public Task<byte[]> GetBytesAsync(string objectKey, CancellationToken cancellationToken)
        => Task.FromResult(Array.Empty<byte>());

    public string? GetPublicUrl(string objectKey)
        => $"/campaign-assets/{Uri.EscapeDataString(objectKey)}";
}

internal sealed class StubWebHostEnvironment : IWebHostEnvironment
{
    public StubWebHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        WebRootPath = contentRootPath;
        ApplicationName = "Advertified.App.Tests";
        EnvironmentName = Environments.Development;
        ContentRootFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentRootPath);
        WebRootFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentRootPath);
    }

    public string ApplicationName { get; set; }

    public IFileProvider WebRootFileProvider { get; set; }

    public string WebRootPath { get; set; }

    public string EnvironmentName { get; set; }

    public string ContentRootPath { get; set; }

    public IFileProvider ContentRootFileProvider { get; set; }
}
