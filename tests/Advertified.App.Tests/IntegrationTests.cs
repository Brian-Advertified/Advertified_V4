using System.Net;
using System.Net.Http.Json;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.AIPlatform.Infrastructure;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Auth;
using Advertified.App.Contracts.Admin;
using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Packages;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
    public async Task PublicLegalDocuments_ReturnsSeededTermsDocumentOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                db.LegalDocuments.Add(new LegalDocument
                {
                    Id = Guid.NewGuid(),
                    DocumentKey = "terms-and-conditions",
                    Title = "Terms and Conditions",
                    VersionLabel = "2026-04-05",
                    BodyJson = "[{\"title\":\"1. Agreement Formation\",\"paragraphs\":[\"Terms body paragraph one.\",\"Terms body paragraph two.\"]}]",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
                db.SaveChanges();
            });

        var response = await harness.Client.GetAsync("/public/legal-documents/terms-and-conditions");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        using var json = System.Text.Json.JsonDocument.Parse(content);
        json.RootElement.GetProperty("documentKey").GetString().Should().Be("terms-and-conditions");
        json.RootElement.GetProperty("title").GetString().Should().Be("Terms and Conditions");
        json.RootElement.GetProperty("sections")[0].GetProperty("title").GetString().Should().Be("1. Agreement Formation");
    }

    [Fact]
    public async Task AuthRegister_CompletesExistingPendingClientAccountOverHttp()
    {
        var prospectEmail = "prospect.client@example.com";

        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var now = DateTime.UtcNow;
                var user = new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Prospect Client",
                    Email = prospectEmail,
                    Phone = "0820001111",
                    IsSaCitizen = true,
                    EmailVerified = false,
                    PhoneVerified = false,
                    Role = UserRole.Client,
                    AccountStatus = AccountStatus.PendingVerification,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                user.PasswordHash = new PasswordHashingService().HashPassword(user, Guid.NewGuid().ToString("N"));

                db.UserAccounts.Add(user);
                db.SaveChanges();
            });

        var request = new RegisterRequest
        {
            FullName = "Prospect Client",
            Email = prospectEmail,
            Phone = "0821234567",
            IsSouthAfricanCitizen = true,
            Password = "StrongPass!123",
            ConfirmPassword = "StrongPass!123",
            BusinessName = "Prospect Client Pty Ltd",
            BusinessType = "PTY LTD",
            RegistrationNumber = "2026/654321/07",
            Industry = "Technology",
            AnnualRevenueBand = "r1m_r5m",
            StreetAddress = "1 Main Road",
            City = "Johannesburg",
            Province = "Gauteng",
            SaIdNumber = "9001011234088"
        };

        var response = await harness.Client.PostAsJsonAsync("/auth/register", request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        var payload = System.Text.Json.JsonSerializer.Deserialize<RegisterResponse>(content, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        payload.Should().NotBeNull();
        payload!.Email.Should().Be(prospectEmail);
        payload.AccountStatus.Should().Be("pending_verification");

        var dbUser = await harness.ExecuteDbAsync(db => db.UserAccounts
            .Include(x => x.BusinessProfile)
            .Include(x => x.IdentityProfile)
            .SingleAsync(x => x.Email == prospectEmail));
        var passwordHasher = new PasswordHashingService();

        dbUser.BusinessProfile.Should().NotBeNull();
        dbUser.BusinessProfile!.BusinessName.Should().Be("Prospect Client Pty Ltd");
        dbUser.IdentityProfile.Should().NotBeNull();
        dbUser.IdentityProfile!.SaIdNumber.Should().Be("9001011234088");
        passwordHasher.VerifyPassword(dbUser, "StrongPass!123").Should().BeTrue();
    }

    [Fact]
    public async Task AiAdOps_ClientCannotPublishVariantOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "approved");
                var variant = new AiAdVariant
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    Platform = "Meta",
                    Channel = "Digital",
                    Language = "English",
                    Script = "Test script",
                    Status = "draft",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.UserAccounts.Add(clientUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.AiAdVariants.Add(variant);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubAdVariantService>();
                services.AddScoped<IAdVariantService>(sp => sp.GetRequiredService<StubAdVariantService>());
            });

        var clientUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Select(x => x.Id).SingleAsync());
        var variantId = await harness.ExecuteDbAsync(db => db.AiAdVariants.Select(x => x.Id).SingleAsync());
        var stub = harness.Services.GetRequiredService<StubAdVariantService>();
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(clientUserId));

        var response = await harness.Client.PostAsJsonAsync($"/api/v2/ai-platform/ad-ops/variants/{variantId}/publish", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stub.PublishCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AiAdOps_AgentNotAssignedCannotAccessVariantsOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var assignedAgent = TestSeed.CreateAgent();
                var otherAgent = TestSeed.CreateAgent();
                assignedAgent.Email = "assigned.agent@advertified.test";
                otherAgent.Email = "other.agent@advertified.test";
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "approved");
                campaign.AssignedAgentUserId = assignedAgent.Id;
                campaign.AssignedAt = DateTime.UtcNow;

                db.UserAccounts.AddRange(clientUser, assignedAgent, otherAgent);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubAdVariantService>();
                services.AddScoped<IAdVariantService>(sp => sp.GetRequiredService<StubAdVariantService>());
            });

        var otherAgentId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts
                .Where(x => x.Role == UserRole.Agent && x.Email == "other.agent@advertified.test")
                .Select(x => x.Id)
                .FirstAsync());
        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var stub = harness.Services.GetRequiredService<StubAdVariantService>();
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(otherAgentId));

        var response = await harness.Client.GetAsync($"/api/v2/ai-platform/ad-ops/campaigns/{campaignId}/variants");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stub.GetCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AiAdOps_UnauthenticatedCannotAccessVariantsOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "approved");
                db.UserAccounts.Add(clientUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubAdVariantService>();
                services.AddScoped<IAdVariantService>(sp => sp.GetRequiredService<StubAdVariantService>());
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var response = await harness.Client.GetAsync($"/api/v2/ai-platform/ad-ops/campaigns/{campaignId}/variants");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AiAdOps_ClientCannotSyncMetricsOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "approved");
                db.UserAccounts.Add(clientUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubAdVariantService>();
                services.AddScoped<IAdVariantService>(sp => sp.GetRequiredService<StubAdVariantService>());
            });

        var clientUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Select(x => x.Id).SingleAsync());
        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var stub = harness.Services.GetRequiredService<StubAdVariantService>();
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(clientUserId));

        var response = await harness.Client.PostAsJsonAsync($"/api/v2/ai-platform/ad-ops/campaigns/{campaignId}/sync-metrics", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stub.SyncCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AiAdOps_ClientCannotOptimizeCampaignOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "approved");
                db.UserAccounts.Add(clientUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubAdVariantService>();
                services.AddScoped<IAdVariantService>(sp => sp.GetRequiredService<StubAdVariantService>());
            });

        var clientUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Select(x => x.Id).SingleAsync());
        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var stub = harness.Services.GetRequiredService<StubAdVariantService>();
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(clientUserId));

        var response = await harness.Client.PostAsJsonAsync($"/api/v2/ai-platform/ad-ops/campaigns/{campaignId}/optimize", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stub.OptimizeCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AiAdOps_ClientCrossTenantCannotReadMetricsSummaryOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var ownerClient = TestSeed.CreateUser();
                ownerClient.Email = "owner.client@advertified.test";
                var otherClient = TestSeed.CreateUser();
                otherClient.Email = "other.client@advertified.test";
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(ownerClient, selectedBudget: 250000m, status: "approved");

                db.UserAccounts.AddRange(ownerClient, otherClient);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubAdVariantService>();
                services.AddScoped<IAdVariantService>(sp => sp.GetRequiredService<StubAdVariantService>());
            });

        var otherClientId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts
                .Where(x => x.Email == "other.client@advertified.test")
                .Select(x => x.Id)
                .SingleAsync());
        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var stub = harness.Services.GetRequiredService<StubAdVariantService>();
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(otherClientId));

        var response = await harness.Client.GetAsync($"/api/v2/ai-platform/ad-ops/campaigns/{campaignId}/metrics/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stub.SummaryCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AiAdOps_ClientCrossTenantCannotTrackConversionsOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var ownerClient = TestSeed.CreateUser();
                ownerClient.Email = "owner.client@advertified.test";
                var otherClient = TestSeed.CreateUser();
                otherClient.Email = "other.client@advertified.test";
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(ownerClient, selectedBudget: 250000m, status: "approved");
                var variant = new AiAdVariant
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    Platform = "Meta",
                    Channel = "Digital",
                    Language = "English",
                    Script = "Cross-tenant conversion test",
                    Status = "published",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    PublishedAt = DateTime.UtcNow
                };

                db.UserAccounts.AddRange(ownerClient, otherClient);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.AiAdVariants.Add(variant);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubAdVariantService>();
                services.AddScoped<IAdVariantService>(sp => sp.GetRequiredService<StubAdVariantService>());
            });

        var otherClientId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts
                .Where(x => x.Email == "other.client@advertified.test")
                .Select(x => x.Id)
                .SingleAsync());
        var variantId = await harness.ExecuteDbAsync(db => db.AiAdVariants.Select(x => x.Id).SingleAsync());
        var stub = harness.Services.GetRequiredService<StubAdVariantService>();
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(otherClientId));

        var response = await harness.Client.PostAsJsonAsync(
            $"/api/v2/ai-platform/ad-ops/variants/{variantId}/conversions",
            new { conversions = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stub.ConversionCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AiAdOps_ClientCrossTenantCannotCreateVariantOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var ownerClient = TestSeed.CreateUser();
                ownerClient.Email = "owner.client@advertified.test";
                var otherClient = TestSeed.CreateUser();
                otherClient.Email = "other.client@advertified.test";
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(ownerClient, selectedBudget: 250000m, status: "approved");

                db.UserAccounts.AddRange(ownerClient, otherClient);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubAdVariantService>();
                services.AddScoped<IAdVariantService>(sp => sp.GetRequiredService<StubAdVariantService>());
            });

        var otherClientId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts
                .Where(x => x.Email == "other.client@advertified.test")
                .Select(x => x.Id)
                .SingleAsync());
        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var stub = harness.Services.GetRequiredService<StubAdVariantService>();
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(otherClientId));

        var response = await harness.Client.PostAsJsonAsync("/api/v2/ai-platform/ad-ops/variants", new
        {
            campaignId,
            platform = "Meta",
            channel = "Digital",
            language = "English",
            script = "Cross-tenant create should be forbidden",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stub.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AiAdOps_ClientCrossTenantCannotGetVariantsOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var ownerClient = TestSeed.CreateUser();
                ownerClient.Email = "owner.client@advertified.test";
                var otherClient = TestSeed.CreateUser();
                otherClient.Email = "other.client@advertified.test";
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(ownerClient, selectedBudget: 250000m, status: "approved");

                db.UserAccounts.AddRange(ownerClient, otherClient);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubAdVariantService>();
                services.AddScoped<IAdVariantService>(sp => sp.GetRequiredService<StubAdVariantService>());
            });

        var otherClientId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts
                .Where(x => x.Email == "other.client@advertified.test")
                .Select(x => x.Id)
                .SingleAsync());
        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var stub = harness.Services.GetRequiredService<StubAdVariantService>();
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(otherClientId));

        var response = await harness.Client.GetAsync($"/api/v2/ai-platform/ad-ops/campaigns/{campaignId}/variants");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stub.GetCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AiAdOps_AdminGetsNotFoundForUnknownCampaignOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var adminUser = TestSeed.CreateAdmin();
                db.UserAccounts.Add(adminUser);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubAdVariantService>();
                services.AddScoped<IAdVariantService>(sp => sp.GetRequiredService<StubAdVariantService>());
            });

        var adminUserId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts.Where(x => x.Role == UserRole.Admin).Select(x => x.Id).SingleAsync());
        var missingCampaignId = Guid.NewGuid();
        var stub = harness.Services.GetRequiredService<StubAdVariantService>();
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var variantsResponse = await harness.Client.GetAsync($"/api/v2/ai-platform/ad-ops/campaigns/{missingCampaignId}/variants");
        variantsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        stub.GetCallCount.Should().Be(0);

        var summaryResponse = await harness.Client.GetAsync($"/api/v2/ai-platform/ad-ops/campaigns/{missingCampaignId}/metrics/summary");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        stub.SummaryCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AiAdOps_AdminGetsNotFoundForUnknownVariantOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var adminUser = TestSeed.CreateAdmin();
                db.UserAccounts.Add(adminUser);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubAdVariantService>();
                services.AddScoped<IAdVariantService>(sp => sp.GetRequiredService<StubAdVariantService>());
            });

        var adminUserId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts.Where(x => x.Role == UserRole.Admin).Select(x => x.Id).SingleAsync());
        var missingVariantId = Guid.NewGuid();
        var stub = harness.Services.GetRequiredService<StubAdVariantService>();
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var publishResponse = await harness.Client.PostAsJsonAsync($"/api/v2/ai-platform/ad-ops/variants/{missingVariantId}/publish", new { });
        publishResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        stub.PublishCallCount.Should().Be(0);

        var conversionResponse = await harness.Client.PostAsJsonAsync(
            $"/api/v2/ai-platform/ad-ops/variants/{missingVariantId}/conversions",
            new { conversions = 1 });
        conversionResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        stub.ConversionCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AiAdOps_EndToEnd_CreatePublishSyncOptimizeOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var adminUser = TestSeed.CreateAdmin();
                var clientUser = TestSeed.CreateUser();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "approved");

                db.UserAccounts.AddRange(adminUser, clientUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.PackageBandAiEntitlements.Add(new PackageBandAiEntitlement
                {
                    PackageBandId = band.Id,
                    MaxAdVariants = 3,
                    AllowedAdPlatformsJson = "[\"Meta\"]",
                    AllowAdMetricsSync = true,
                    AllowAdAutoOptimize = true,
                    AllowedVoicePackTiersJson = "[\"standard\",\"premium\",\"exclusive\"]",
                    MaxAdRegenerations = 3,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddHttpClient();
                services.Configure<AiPlatformOptions>(options =>
                {
                    options.UseInMemoryFallback = true;
                    options.ServiceBusConnectionString = string.Empty;
                });
                services.Configure<AdPlatformOptions>(options =>
                {
                    options.DryRunMode = true;
                    options.Meta.Enabled = false;
                    options.GoogleAds.Enabled = false;
                });
                services.AddAiAdvertisingPlatform();
            });

        var adminUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Admin).Select(x => x.Id).SingleAsync());
        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var createResponse = await harness.Client.PostAsJsonAsync("/api/v2/ai-platform/ad-ops/variants", new
        {
            campaignId,
            platform = "Meta",
            channel = "Digital",
            language = "English",
            script = "Advertified gets your campaign live fast.",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<AdVariantApiTestResponse>();
        created.Should().NotBeNull();

        var publishResponse = await harness.Client.PostAsJsonAsync($"/api/v2/ai-platform/ad-ops/variants/{created!.Id}/publish", new { });
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var syncResponse = await harness.Client.PostAsJsonAsync($"/api/v2/ai-platform/ad-ops/campaigns/{campaignId}/sync-metrics", new { });
        syncResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var optimizeResponse = await harness.Client.PostAsJsonAsync($"/api/v2/ai-platform/ad-ops/campaigns/{campaignId}/optimize", new { });
        optimizeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AiAdOps_PublishAutoStopsWhenCostCapBreaches()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var adminUser = TestSeed.CreateAdmin();
                var clientUser = TestSeed.CreateUser();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "approved");

                db.UserAccounts.AddRange(adminUser, clientUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.PackageBandAiEntitlements.Add(new PackageBandAiEntitlement
                {
                    PackageBandId = band.Id,
                    MaxAdVariants = 3,
                    AllowedAdPlatformsJson = "[\"Meta\"]",
                    AllowAdMetricsSync = true,
                    AllowAdAutoOptimize = true,
                    AllowedVoicePackTiersJson = "[\"standard\",\"premium\",\"exclusive\"]",
                    MaxAdRegenerations = 3,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddHttpClient();
                services.Configure<AiPlatformOptions>(options =>
                {
                    options.UseInMemoryFallback = true;
                    options.ServiceBusConnectionString = string.Empty;
                    options.MaxAiCostHardCapZar = 0m;
                    options.MaxAiCostPercentOfCampaignBudget = 0m;
                });
                services.Configure<AdPlatformOptions>(options =>
                {
                    options.DryRunMode = true;
                    options.Meta.Enabled = false;
                    options.GoogleAds.Enabled = false;
                });
                services.AddAiAdvertisingPlatform();
            });

        var adminUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Admin).Select(x => x.Id).SingleAsync());
        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var createResponse = await harness.Client.PostAsJsonAsync("/api/v2/ai-platform/ad-ops/variants", new
        {
            campaignId,
            platform = "Meta",
            channel = "Digital",
            language = "English",
            script = "Advertified publish test under strict cap.",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<AdVariantApiTestResponse>();
        created.Should().NotBeNull();

        var publishResponse = await harness.Client.PostAsJsonAsync($"/api/v2/ai-platform/ad-ops/variants/{created!.Id}/publish", new { });
        publishResponse.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var variantStatus = await harness.ExecuteDbAsync(db => db.AiAdVariants.Where(x => x.Id == created.Id).Select(x => x.Status).SingleAsync());
        variantStatus.Should().Be("cost_stopped");
    }

    [Fact]
    public async Task AdminAiReplay_CreativeFailedJob_CanReplay()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var adminUser = TestSeed.CreateAdmin();
                var clientUser = TestSeed.CreateUser();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "approved");
                var failedJobId = Guid.NewGuid();

                db.UserAccounts.AddRange(adminUser, clientUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.AiCreativeJobStatuses.Add(new AiCreativeJobStatus
                {
                    JobId = failedJobId,
                    CampaignId = campaign.Id,
                    Status = "failed",
                    Error = "provider timeout",
                    RetryAttemptCount = 3,
                    LastFailure = "provider timeout",
                    UpdatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
            });

        var adminUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Admin).Select(x => x.Id).SingleAsync());
        var failedJobId = await harness.ExecuteDbAsync(db => db.AiCreativeJobStatuses.Select(x => x.JobId).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var replayResponse = await harness.Client.PostAsJsonAsync($"/admin/ai/jobs/creative/{failedJobId}/replay", new { });

        replayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminAiMonthlyCostReport_ClientUserIsForbidden()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var adminUser = TestSeed.CreateAdmin();
                var clientUser = TestSeed.CreateUser();
                db.UserAccounts.AddRange(adminUser, clientUser);
                db.SaveChanges();
            });

        var clientUserId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts.Where(x => x.Role == UserRole.Client).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(clientUserId));

        var response = await harness.Client.GetAsync("/admin/ai/cost-reports/monthly?months=3");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminAiReplay_AssetFailedJob_ClientUserIsForbiddenAndJobUnchanged()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var adminUser = TestSeed.CreateAdmin();
                var clientUser = TestSeed.CreateUser();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "approved");
                var failedAssetJobId = Guid.NewGuid();

                db.UserAccounts.AddRange(adminUser, clientUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.AiAssetJobs.Add(new AiAssetJob
                {
                    Id = failedAssetJobId,
                    CampaignId = campaign.Id,
                    CreativeId = Guid.NewGuid(),
                    AssetKind = "voice",
                    Provider = "ElevenLabs",
                    Status = "failed",
                    RequestJson = "{}",
                    Error = "provider timeout",
                    RetryAttemptCount = 3,
                    LastFailure = "provider timeout",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
            });

        var clientUserId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts.Where(x => x.Role == UserRole.Client).Select(x => x.Id).SingleAsync());
        var failedAssetJobId = await harness.ExecuteDbAsync(db => db.AiAssetJobs.Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(clientUserId));

        var replayResponse = await harness.Client.PostAsJsonAsync($"/admin/ai/jobs/assets/{failedAssetJobId}/replay", new { });

        replayResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var status = await harness.ExecuteDbAsync(db =>
            db.AiAssetJobs.Where(x => x.Id == failedAssetJobId).Select(x => x.Status).SingleAsync());
        status.Should().Be("failed");
    }

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
    public async Task PublicProposalApproval_SendsApprovalSideEffectsOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "review_ready");
                campaign.AssignedAgentUserId = agentUser.Id;
                campaign.AssignedAt = DateTime.UtcNow;

                var recommendation = TestSeed.CreateRecommendation(campaign.Id, status: "sent_to_client");

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
        var recommendationId = await harness.ExecuteDbAsync(db => db.CampaignRecommendations.Select(x => x.Id).SingleAsync());
        await using var scope = harness.Services.CreateAsyncScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IProposalAccessTokenService>();
        var token = tokenService.CreateToken(campaignId);

        var approveResponse = await harness.Client.PostAsJsonAsync($"/public/proposals/{campaignId}/approve", new
        {
            token,
            recommendationId
        });

        approveResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var approvedCampaign = await harness.ExecuteDbAsync(db => db.Campaigns.Include(x => x.CampaignRecommendations).SingleAsync(x => x.Id == campaignId));
        approvedCampaign.Status.Should().Be("approved");
        approvedCampaign.CampaignRecommendations.Should().Contain(x => x.Status == "approved");

        var emailService = harness.Services.GetRequiredService<StubTemplatedEmailService>();
        emailService.SentEmails.Select(x => x.TemplateName).Should().Contain(new[]
        {
            "recommendation-approved",
            "activation-in-progress",
            "creative-queue-update"
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
    public async Task AgentGenerateRecommendation_UsesAscendingBandBudgetsForProposalVariants()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var band = new PackageBand
                {
                    Id = Guid.NewGuid(),
                    Code = "launch",
                    Name = "Launch",
                    MinBudget = 25000m,
                    MaxBudget = 100000m,
                    SortOrder = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                var order = new PackageOrder
                {
                    Id = Guid.NewGuid(),
                    UserId = clientUser.Id,
                    PackageBandId = band.Id,
                    Amount = 25000m,
                    SelectedBudget = 25000m,
                    Currency = "ZAR",
                    PaymentStatus = "paid",
                    RefundStatus = "none",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                var campaign = new Campaign
                {
                    Id = Guid.NewGuid(),
                    UserId = clientUser.Id,
                    PackageOrderId = order.Id,
                    PackageBandId = band.Id,
                    CampaignName = "Banded recommendation campaign",
                    Status = "planning_in_progress",
                    AiUnlocked = true,
                    AgentAssistanceRequested = true,
                    AssignedAgentUserId = agentUser.Id,
                    AssignedAt = DateTime.UtcNow,
                    PlanningMode = "hybrid",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CampaignBrief = new CampaignBrief
                    {
                        Id = Guid.NewGuid(),
                        Objective = "launch",
                        GeographyScope = "regional",
                        ProvincesJson = "[\"Gauteng\"]",
                        PreferredMediaTypesJson = "[\"radio\",\"ooh\"]",
                        OpenToUpsell = false,
                        SubmittedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                };
                campaign.CampaignBrief.CampaignId = campaign.Id;

                db.UserAccounts.AddRange(clientUser, agentUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var agentUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Agent).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(agentUserId));

        var response = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/generate-recommendation", new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recommendations = await harness.ExecuteDbAsync(db => db.CampaignRecommendations
            .Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.RecommendationType, x.TotalCost })
            .ToListAsync());

        recommendations.Should().HaveCount(3);
        recommendations.Select(x => x.TotalCost).Should().ContainInOrder(37500m, 62500m, 87500m);
        recommendations.Select(x => x.RecommendationType).Should().Contain(new[]
        {
            "hybrid:balanced",
            "hybrid:ooh_focus",
            "hybrid:radio_focus"
        });
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

    [Fact]
    public async Task AdminCampaignOperations_UnauthenticatedRequest_ReturnsUnauthorized()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var adminUser = TestSeed.CreateAdmin();
                db.UserAccounts.Add(adminUser);
                db.SaveChanges();
            });

        var response = await harness.Client.GetAsync("/admin/campaign-operations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminPackageOrders_ClientUserIsForbidden()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var adminUser = TestSeed.CreateAdmin();
                var clientUser = TestSeed.CreateUser();
                db.UserAccounts.AddRange(adminUser, clientUser);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<IPackagePurchaseService, StubPackagePurchaseService>();
                services.AddSingleton<IPrivateDocumentStorage, StubPrivateDocumentStorage>();
                services.AddSingleton<IInvoiceService, StubInvoiceService>();
                services.AddSingleton<ITemplatedEmailService, StubTemplatedEmailService>();
            });

        var clientUserId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts.Where(x => x.Role == UserRole.Client).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(clientUserId));

        var response = await harness.Client.GetAsync("/admin/package-orders");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

internal sealed class StubAdminDashboardService : IAdminDashboardService
{
    public Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<AdminOutletPageResponse> GetOutletPageAsync(int page, int pageSize, bool issuesOnly, string sortBy, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}

internal sealed class StubAdminMutationService : IAdminMutationService
{
    public Task<AdminOutletDetailResponse> GetOutletAsync(string code, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<AdminOutletPricingResponse> GetOutletPricingAsync(string code, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<AdminOutletMutationResponse> CreateOutletAsync(CreateAdminOutletRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<AdminOutletMutationResponse> UpdateOutletAsync(string existingCode, UpdateAdminOutletRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task DeleteOutletAsync(string code, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<Guid> CreateOutletPricingPackageAsync(string code, UpsertAdminOutletPricingPackageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task UpdateOutletPricingPackageAsync(string code, Guid packageId, UpsertAdminOutletPricingPackageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task DeleteOutletPricingPackageAsync(string code, Guid packageId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<Guid> CreateOutletSlotRateAsync(string code, UpsertAdminOutletSlotRateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task UpdateOutletSlotRateAsync(string code, Guid slotRateId, UpsertAdminOutletSlotRateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task DeleteOutletSlotRateAsync(string code, Guid slotRateId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<AdminGeographyDetailResponse> GetGeographyAsync(string code, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<AdminGeographyDetailResponse> CreateGeographyAsync(CreateAdminGeographyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<AdminGeographyDetailResponse> UpdateGeographyAsync(string existingCode, UpdateAdminGeographyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task DeleteGeographyAsync(string code, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<Guid> CreateGeographyMappingAsync(string code, UpsertAdminGeographyMappingRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task UpdateGeographyMappingAsync(string code, Guid mappingId, UpsertAdminGeographyMappingRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task DeleteGeographyMappingAsync(string code, Guid mappingId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<AdminRateCardUploadResponse> UploadRateCardAsync(string channel, string? supplierOrStation, string? documentTitle, string? notes, IFormFile file, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task UpdateRateCardAsync(string sourceFile, UpdateAdminRateCardRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task DeleteRateCardAsync(string sourceFile, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<Guid> CreatePackageSettingAsync(CreateAdminPackageSettingRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task UpdatePackageSettingAsync(Guid packageSettingId, UpdateAdminPackageSettingRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task DeletePackageSettingAsync(Guid packageSettingId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task UpdatePricingSettingsAsync(UpdateAdminPricingSettingsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task UpdateEnginePolicyAsync(string packageCode, UpdateAdminEnginePolicyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task UpdatePreviewRuleAsync(string packageCode, string tierCode, UpdateAdminPreviewRuleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class StubAdVariantService : IAdVariantService
{
    public int CreateCallCount { get; private set; }
    public int PublishCallCount { get; private set; }
    public int GetCallCount { get; private set; }
    public int SyncCallCount { get; private set; }
    public int OptimizeCallCount { get; private set; }
    public int SummaryCallCount { get; private set; }
    public int ConversionCallCount { get; private set; }

    public Task<AdVariantSummary> CreateVariantAsync(CreateAdVariantCommand command, CancellationToken cancellationToken)
    {
        CreateCallCount++;
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new AdVariantSummary(
            Guid.NewGuid(),
            command.CampaignId,
            command.CampaignCreativeId,
            command.Platform,
            command.Channel,
            command.Language,
            command.TemplateId,
            command.VoicePackId,
            command.VoicePackName,
            command.Script,
            command.AudioAssetUrl,
            null,
            "draft",
            now,
            now,
            null));
    }

    public Task<IReadOnlyList<AdVariantSummary>> GetCampaignVariantsAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        GetCallCount++;
        return Task.FromResult<IReadOnlyList<AdVariantSummary>>(Array.Empty<AdVariantSummary>());
    }

    public Task<PublishAdVariantResult> PublishVariantAsync(Guid variantId, CancellationToken cancellationToken)
    {
        PublishCallCount++;
        return Task.FromResult(new PublishAdVariantResult(
            variantId,
            Guid.NewGuid(),
            "Meta",
            $"meta-{variantId:D}",
            "published",
            DateTimeOffset.UtcNow));
    }

    public Task RecordConversionAsync(Guid variantId, int conversions, CancellationToken cancellationToken)
    {
        ConversionCallCount++;
        return Task.CompletedTask;
    }

    public Task<CampaignAdMetricsSummary> GetCampaignMetricsSummaryAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        SummaryCallCount++;
        return Task.FromResult(new CampaignAdMetricsSummary(campaignId, 0, 0, 0, 0, 0, 0m, 0m, 0m, null, null, null));
    }

    public Task<SyncCampaignMetricsResult> SyncCampaignMetricsAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        SyncCallCount++;
        return Task.FromResult(new SyncCampaignMetricsResult(campaignId, 0, new CampaignAdMetricsSummary(campaignId, 0, 0, 0, 0, 0, 0m, 0m, 0m, null, null, null)));
    }

    public Task<int> SyncAllPublishedCampaignsAsync(CancellationToken cancellationToken)
        => Task.FromResult(0);

    public Task<OptimizeCampaignResult> OptimizeCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        OptimizeCallCount++;
        return Task.FromResult(new OptimizeCampaignResult(campaignId, null, "stub", DateTimeOffset.UtcNow));
    }
}

internal sealed class AdVariantApiTestResponse
{
    public Guid Id { get; set; }
}

internal sealed class StubCreativeCampaignOrchestrator : ICreativeCampaignOrchestrator
{
    public Task<GenerateCampaignCreativesResult> GenerateAsync(
        GenerateCampaignCreativesCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new GenerateCampaignCreativesResult(
            Guid.NewGuid(),
            command.CampaignId,
            Array.Empty<CreativeVariant>(),
            Array.Empty<CreativeQualityScore>(),
            Array.Empty<AssetGenerationResult>(),
            DateTimeOffset.UtcNow));
    }

    public Task<QueueCreativeJobStatus> QueueGenerationAsync(
        GenerateCampaignCreativesCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new QueueCreativeJobStatus(
            Guid.NewGuid(),
            command.CampaignId,
            "queued",
            null,
            0,
            null,
            DateTimeOffset.UtcNow));
    }
}

internal sealed class StubAssetJobQueue : IAssetJobQueue
{
    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public async IAsyncEnumerable<AssetJobEnvelope> DequeueAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
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
        builder.Services.AddScoped<IProposalAccessTokenService, ProposalAccessTokenService>();
        builder.Services.AddScoped<IPasswordHashingService, PasswordHashingService>();
        builder.Services.AddScoped<IChangeAuditService, ChangeAuditService>();
        builder.Services.AddScoped<IAdminDashboardService, StubAdminDashboardService>();
        builder.Services.AddScoped<IAdminMutationService, StubAdminMutationService>();
        builder.Services.AddScoped<ICampaignAccessService, CampaignAccessService>();
        builder.Services.AddScoped<IAgentAreaRoutingService, StubAgentAreaRoutingService>();
        builder.Services.AddScoped<ICampaignBriefService, CampaignBriefService>();
        builder.Services.AddScoped<CampaignPlanningRequestValidator>();
        builder.Services.AddScoped<RegisterRequestValidator>();
        builder.Services.AddScoped<SaveCampaignBriefRequestValidator>();
        builder.Services.AddScoped<IEmailVerificationService, StubEmailVerificationService>();
        builder.Services.AddScoped<IInvoiceService, StubInvoiceService>();
        builder.Services.AddScoped<IPackagePurchaseService, StubPackagePurchaseService>();
        builder.Services.AddScoped<IRegistrationService, RegistrationService>();
        builder.Services.AddScoped<IPrivateDocumentStorage, StubPrivateDocumentStorage>();
        builder.Services.AddScoped<IRecommendationDocumentService, StubRecommendationDocumentService>();
        builder.Services.AddScoped<IRecommendationApprovalWorkflowService, RecommendationApprovalWorkflowService>();
        builder.Services.AddScoped<IPublicAssetStorage, StubPublicAssetStorage>();
        builder.Services.AddScoped<IMediaPlanningEngine, StubMediaPlanningEngine>();
        builder.Services.AddScoped<ICampaignReasoningService, StubCampaignReasoningService>();
        builder.Services.AddScoped<ICampaignRecommendationService, CampaignRecommendationService>();
        builder.Services.AddScoped<ICampaignBriefInterpretationService, StubCampaignBriefInterpretationService>();
        builder.Services.AddScoped<ITemplatedEmailService, StubTemplatedEmailService>();
        builder.Services.AddScoped<ICreativeCampaignOrchestrator, StubCreativeCampaignOrchestrator>();
        builder.Services.AddSingleton<IAssetJobQueue, StubAssetJobQueue>();

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
                BusinessName = "Advertified (Pty) Ltd",
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
        var recommendationId = Guid.NewGuid();
        return new CampaignRecommendation
        {
            Id = recommendationId,
            CampaignId = campaignId,
            RecommendationType = "hybrid",
            GeneratedBy = "system",
            Status = status,
            TotalCost = 250000m,
            Summary = "Recommended mix",
            Rationale = "Plan built within budget.",
            CreatedAt = now,
            UpdatedAt = now,
            RecommendationItems = new List<RecommendationItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RecommendationId = recommendationId,
                    InventoryType = "OOH",
                    DisplayName = "Sandton Digital Billboard",
                    Quantity = 1,
                    UnitCost = 250000m,
                    TotalCost = 250000m,
                    CreatedAt = now
                }
            }
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

    public Task<byte[]> GetRecommendationPdfBytesAsync(Guid campaignId, Guid recommendationId, CancellationToken cancellationToken)
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
                    SourceType = "ooh",
                    DisplayName = "Sandton Digital Billboard",
                    MediaType = "OOH",
                    UnitCost = request.SelectedBudget,
                    Quantity = 1,
                    Score = 91m,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["selectionReasons"] = new[] { "OOH-first package fit", "Matches requested channel mix" },
                        ["confidenceScore"] = 0.86m,
                        ["province"] = "Gauteng",
                        ["city"] = "Johannesburg",
                        ["area"] = "Sandton"
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
    public Task QueueActivationEmailAsync(UserAccount user, string? nextPath, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<UserAccount> VerifyAsync(string token, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task ResendActivationAsync(string email, string? nextPath, CancellationToken cancellationToken) => Task.CompletedTask;
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

internal sealed class StubPackagePurchaseService : IPackagePurchaseService
{
    public Task<CreatePackageOrderResponse> CreatePendingOrderAsync(Guid userId, CreatePackageOrderRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<CreatePackageOrderResponse> InitiateCheckoutAsync(
        Guid userId,
        Guid packageOrderId,
        string paymentProvider,
        Guid? recommendationId,
        CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task PrepareRecommendationCheckoutAsync(Guid campaignId, Guid recommendationId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task MarkOrderPaidAsync(Guid packageOrderId, string paymentReference, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task MarkOrderFailedAsync(Guid packageOrderId, string? paymentReference, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

internal sealed class StubPrivateDocumentStorage : IPrivateDocumentStorage
{
    public Task<string> SaveAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken)
        => Task.FromResult(objectKey);

    public Task<byte[]> GetBytesAsync(string objectKey, CancellationToken cancellationToken)
        => Task.FromResult(Array.Empty<byte>());
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
