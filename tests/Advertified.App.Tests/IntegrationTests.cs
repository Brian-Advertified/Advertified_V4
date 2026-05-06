using System.Net;
using System.Net.Http.Json;
using Advertified.App.AIPlatform.Api;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.AIPlatform.Infrastructure;
using Advertified.App.Authentication;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Auth;
using Advertified.App.Contracts.Admin;
using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Contracts.Packages;
using Advertified.App.Contracts.Payments;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Advertified.App.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
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
    public async Task AuthLogin_SetsSessionCookie_AndAllowsMeEndpointWithCookie()
    {
        const string email = "cookie.user@example.com";
        const string password = "StrongPass!123";

        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var now = DateTime.UtcNow;
                var user = new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Cookie User",
                    Email = email,
                    Phone = "0821112222",
                    IsSaCitizen = true,
                    EmailVerified = true,
                    PhoneVerified = true,
                    Role = UserRole.Client,
                    AccountStatus = AccountStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                user.PasswordHash = new PasswordHashingService().HashPassword(user, password);
                db.UserAccounts.Add(user);
                db.SaveChanges();
            });

        var loginResponse = await harness.Client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        var loginContent = await loginResponse.Content.ReadAsStringAsync();

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, loginContent);
        loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders).Should().BeTrue();
        var sessionCookie = ExtractCookieHeader(setCookieHeaders!, SessionCookieDefaults.CookieName);
        sessionCookie.Should().NotBeNullOrWhiteSpace();

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/me");
        meRequest.Headers.Add("Cookie", sessionCookie!);
        var meResponse = await harness.Client.SendAsync(meRequest);
        var meContent = await meResponse.Content.ReadAsStringAsync();

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK, meContent);
        using var meJson = System.Text.Json.JsonDocument.Parse(meContent);
        meJson.RootElement.GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task AuthLogout_ClearsSessionCookie_AndBlocksMeEndpoint()
    {
        const string email = "logout.user@example.com";
        const string password = "StrongPass!123";

        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var now = DateTime.UtcNow;
                var user = new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Logout User",
                    Email = email,
                    Phone = "0823334444",
                    IsSaCitizen = true,
                    EmailVerified = true,
                    PhoneVerified = true,
                    Role = UserRole.Client,
                    AccountStatus = AccountStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                user.PasswordHash = new PasswordHashingService().HashPassword(user, password);
                db.UserAccounts.Add(user);
                db.SaveChanges();
            });

        var loginResponse = await harness.Client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, loginContent);
        loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders).Should().BeTrue();
        var sessionCookie = ExtractCookieHeader(setCookieHeaders!, SessionCookieDefaults.CookieName);
        sessionCookie.Should().NotBeNullOrWhiteSpace();

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Add("Cookie", sessionCookie!);
        logoutRequest.Headers.Add("Origin", "http://localhost:5173");
        var logoutResponse = await harness.Client.SendAsync(logoutRequest);
        var logoutContent = await logoutResponse.Content.ReadAsStringAsync();
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK, logoutContent);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/me");
        var meResponse = await harness.Client.SendAsync(meRequest);
        meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SessionCookie_BecomesInvalidAfterPasswordHashChange()
    {
        const string email = "expiry.user@example.com";
        const string password = "StrongPass!123";

        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var now = DateTime.UtcNow;
                var user = new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Expiry User",
                    Email = email,
                    Phone = "0825556666",
                    IsSaCitizen = true,
                    EmailVerified = true,
                    PhoneVerified = true,
                    Role = UserRole.Client,
                    AccountStatus = AccountStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                user.PasswordHash = new PasswordHashingService().HashPassword(user, password);
                db.UserAccounts.Add(user);
                db.SaveChanges();
            });

        var loginResponse = await harness.Client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, loginContent);
        loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders).Should().BeTrue();
        var sessionCookie = ExtractCookieHeader(setCookieHeaders!, SessionCookieDefaults.CookieName);
        sessionCookie.Should().NotBeNullOrWhiteSpace();

        // Validate baseline authenticated request succeeds with fresh cookie.
        using (var meRequest = new HttpRequestMessage(HttpMethod.Get, "/me"))
        {
            meRequest.Headers.Add("Cookie", sessionCookie!);
            var meResponse = await harness.Client.SendAsync(meRequest);
            meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Rotate password hash, which should invalidate existing session payloads.
        await harness.ExecuteDbAsync(async db =>
        {
            var user = await db.UserAccounts.SingleAsync(x => x.Email == email);
            user.PasswordHash = new PasswordHashingService().HashPassword(user, "DifferentPass!456");
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return true;
        });

        using var staleCookieRequest = new HttpRequestMessage(HttpMethod.Get, "/me");
        staleCookieRequest.Headers.Add("Cookie", sessionCookie!);
        var staleCookieResponse = await harness.Client.SendAsync(staleCookieRequest);

        staleCookieResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedCookieWrite_WithUntrustedOrigin_IsBlocked()
    {
        const string email = "csrf.user@example.com";
        const string password = "StrongPass!123";

        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var now = DateTime.UtcNow;
                var user = new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Csrf User",
                    Email = email,
                    Phone = "0827778888",
                    IsSaCitizen = true,
                    EmailVerified = true,
                    PhoneVerified = true,
                    Role = UserRole.Client,
                    AccountStatus = AccountStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                user.PasswordHash = new PasswordHashingService().HashPassword(user, password);
                db.UserAccounts.Add(user);
                db.SaveChanges();
            });

        var loginResponse = await harness.Client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, loginContent);
        loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders).Should().BeTrue();
        var sessionCookie = ExtractCookieHeader(setCookieHeaders!, SessionCookieDefaults.CookieName);
        sessionCookie.Should().NotBeNullOrWhiteSpace();

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Add("Cookie", sessionCookie!);
        logoutRequest.Headers.Add("Origin", "https://evil.example");

        var logoutResponse = await harness.Client.SendAsync(logoutRequest);

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuthenticatedCookieWrite_WithoutOriginOrReferer_IsBlocked()
    {
        const string email = "csrf.no-origin@example.com";
        const string password = "StrongPass!123";

        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var now = DateTime.UtcNow;
                var user = new UserAccount
                {
                    Id = Guid.NewGuid(),
                    FullName = "Csrf Missing Origin User",
                    Email = email,
                    Phone = "0829990000",
                    IsSaCitizen = true,
                    EmailVerified = true,
                    PhoneVerified = true,
                    Role = UserRole.Client,
                    AccountStatus = AccountStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                user.PasswordHash = new PasswordHashingService().HashPassword(user, password);
                db.UserAccounts.Add(user);
                db.SaveChanges();
            });

        var loginResponse = await harness.Client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, loginContent);
        loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders).Should().BeTrue();
        var sessionCookie = ExtractCookieHeader(setCookieHeaders!, SessionCookieDefaults.CookieName);
        sessionCookie.Should().NotBeNullOrWhiteSpace();

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Add("Cookie", sessionCookie!);
        var logoutResponse = await harness.Client.SendAsync(logoutRequest);

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string? ExtractCookieHeader(IEnumerable<string> setCookieHeaders, string cookieName)
    {
        var prefix = $"{cookieName}=";
        foreach (var header in setCookieHeaders)
        {
            if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var valueEnd = header.IndexOf(';');
            var cookiePair = valueEnd >= 0 ? header[..valueEnd] : header;
            return cookiePair;
        }

        return null;
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
    public async Task AdminPricingSettingsUpdate_WritesChannelMarkupsToChangeAuditOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var adminUser = TestSeed.CreateAdmin();
                db.UserAccounts.Add(adminUser);
                db.SaveChanges();
            });

        var adminUserId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts
                .Where(x => x.Role == UserRole.Admin)
                .Select(x => x.Id)
                .SingleAsync());

        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var response = await harness.Client.PutAsJsonAsync("/admin/pricing-settings", new UpdateAdminPricingSettingsRequest
        {
            AiStudioReservePercent = 0.10m,
            OohMarkupPercent = 0.15m,
            RadioMarkupPercent = 0.15m,
            TvMarkupPercent = 0.15m,
            NewspaperMarkupPercent = 0.15m,
            DigitalMarkupPercent = 0.15m,
            SalesCommissionPercent = 0.10m,
            SalesCommissionThresholdZar = 250000m,
            SalesAgentShareBelowThresholdPercent = 0.60m,
            SalesAgentShareAtOrAboveThresholdPercent = 0.50m
        });

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, content);

        var audit = await harness.ExecuteDbAsync(db => db.ChangeAuditLogs.SingleAsync());
        audit.EntityType.Should().Be("pricing_settings");
        audit.Action.Should().Be("update");
        audit.EntityId.Should().Be("default");
        audit.Summary.Should().Be("Updated platform pricing settings.");
        audit.MetadataJson.Should().NotBeNullOrWhiteSpace();

        using var metadata = System.Text.Json.JsonDocument.Parse(audit.MetadataJson!);
        metadata.RootElement.GetProperty("AiStudioReservePercent").GetDecimal().Should().Be(0.10m);
        metadata.RootElement.GetProperty("OohMarkupPercent").GetDecimal().Should().Be(0.15m);
        metadata.RootElement.GetProperty("RadioMarkupPercent").GetDecimal().Should().Be(0.15m);
        metadata.RootElement.GetProperty("TvMarkupPercent").GetDecimal().Should().Be(0.15m);
        metadata.RootElement.GetProperty("NewspaperMarkupPercent").GetDecimal().Should().Be(0.15m);
        metadata.RootElement.GetProperty("DigitalMarkupPercent").GetDecimal().Should().Be(0.15m);
        metadata.RootElement.GetProperty("SalesCommissionPercent").GetDecimal().Should().Be(0.10m);
        metadata.RootElement.GetProperty("SalesCommissionThresholdZar").GetDecimal().Should().Be(250000m);
        metadata.RootElement.GetProperty("SalesAgentShareBelowThresholdPercent").GetDecimal().Should().Be(0.60m);
        metadata.RootElement.GetProperty("SalesAgentShareAtOrAboveThresholdPercent").GetDecimal().Should().Be(0.50m);
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
        publishResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

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
        var assignContent = await assignResponse.Content.ReadAsStringAsync();
        assignResponse.StatusCode.Should().Be(HttpStatusCode.Accepted, assignContent);

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
        inboxAfterUnassign.UnassignedCount.Should().Be(0);
        inboxAfterUnassign.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AgentCampaignReadEndpoints_HideOtherAgentsWorkOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var assignedAgent = TestSeed.CreateAgent();
                assignedAgent.Email = "assigned.agent@advertified.test";
                var otherAgent = TestSeed.CreateAgent();
                otherAgent.Email = "other.agent@advertified.test";
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 200000m, status: "planning_in_progress");
                campaign.AssignedAgentUserId = assignedAgent.Id;
                campaign.AssignedAt = DateTime.UtcNow;

                db.UserAccounts.AddRange(clientUser, assignedAgent, otherAgent);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var otherAgentId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts
                .Where(x => x.Role == UserRole.Agent && x.Email == "other.agent@advertified.test")
                .Select(x => x.Id)
                .SingleAsync());

        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(otherAgentId));

        var listHttpResponse = await harness.Client.GetAsync("/agent/campaigns");
        var listContent = await listHttpResponse.Content.ReadAsStringAsync();
        listHttpResponse.StatusCode.Should().Be(HttpStatusCode.OK, listContent);
        var listResponse = System.Text.Json.JsonSerializer.Deserialize<CampaignListItemResponse[]>(listContent, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        listResponse.Should().NotBeNull();
        listResponse!.Should().BeEmpty();

        var inboxHttpResponse = await harness.Client.GetAsync("/agent/campaigns/inbox");
        var inboxContent = await inboxHttpResponse.Content.ReadAsStringAsync();
        inboxHttpResponse.StatusCode.Should().Be(HttpStatusCode.OK, inboxContent);
        var inboxResponse = System.Text.Json.JsonSerializer.Deserialize<AgentInboxResponse>(inboxContent, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        inboxResponse.Should().NotBeNull();
        inboxResponse!.Items.Should().BeEmpty();

        var detailResponse = await harness.Client.GetAsync($"/agent/campaigns/{campaignId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AgentCannotCreateRecommendationOnAnotherAgentsCampaignOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var assignedAgent = TestSeed.CreateAgent();
                assignedAgent.Email = "assigned.agent@advertified.test";
                var otherAgent = TestSeed.CreateAgent();
                otherAgent.Email = "other.agent@advertified.test";
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "planning_in_progress");
                campaign.AssignedAgentUserId = assignedAgent.Id;
                campaign.AssignedAt = DateTime.UtcNow;

                db.UserAccounts.AddRange(clientUser, assignedAgent, otherAgent);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var otherAgentId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts
                .Where(x => x.Role == UserRole.Agent && x.Email == "other.agent@advertified.test")
                .Select(x => x.Id)
                .SingleAsync());

        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(otherAgentId));

        var response = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/recommendations", new
        {
            notes = "Unauthorized draft",
            inventoryItems = new[]
            {
                new
                {
                    id = "slot-1",
                    type = "ooh",
                    station = "Sandton Digital Billboard",
                    quantity = 1,
                    rate = 125000m,
                    region = "Gauteng",
                    language = "English",
                    showDaypart = "Drive",
                    timeBand = "06:00-09:00",
                    slotType = "Billboard",
                    duration = "4 weeks"
                }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var recommendationCount = await harness.ExecuteDbAsync(db => db.CampaignRecommendations.CountAsync());
        recommendationCount.Should().Be(0);
    }

    [Fact]
    public async Task AgentProspectRegistration_BlocksDuplicateLeadAcrossAgentsOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var now = DateTime.UtcNow;
                var assignedAgent = TestSeed.CreateAgent();
                assignedAgent.Email = "assigned.agent@advertified.test";
                var otherAgent = TestSeed.CreateAgent();
                otherAgent.Email = "other.agent@advertified.test";
                var lead = new ProspectLead
                {
                    Id = Guid.NewGuid(),
                    FullName = "Duplicate Lead",
                    Email = "duplicate.lead@example.com",
                    NormalizedEmail = "duplicate.lead@example.com",
                    Phone = "0821234567",
                    NormalizedPhone = "+27821234567",
                    Source = "agent_prospect",
                    OwnerAgentUserId = assignedAgent.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var band = new PackageBand
                {
                    Id = Guid.NewGuid(),
                    Code = "scale",
                    Name = "Scale",
                    MinBudget = 150000m,
                    MaxBudget = 500000m,
                    SortOrder = 3,
                    IsActive = true,
                    CreatedAt = now
                };
                var order = new PackageOrder
                {
                    Id = Guid.NewGuid(),
                    ProspectLeadId = lead.Id,
                    PackageBandId = band.Id,
                    OrderIntent = OrderIntentValues.Prospect,
                    Amount = 150000m,
                    SelectedBudget = 150000m,
                    Currency = "ZAR",
                    PaymentStatus = "pending",
                    RefundStatus = "none",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var campaign = new Campaign
                {
                    Id = Guid.NewGuid(),
                    ProspectLeadId = lead.Id,
                    PackageOrderId = order.Id,
                    PackageBandId = band.Id,
                    CampaignName = "Duplicate lead campaign",
                    Status = "awaiting_purchase",
                    AssignedAgentUserId = assignedAgent.Id,
                    AssignedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                db.UserAccounts.AddRange(assignedAgent, otherAgent);
                db.ProspectLeads.Add(lead);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            });

        var otherAgentId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts
                .Where(x => x.Role == UserRole.Agent && x.Email == "other.agent@advertified.test")
                .Select(x => x.Id)
                .SingleAsync());
        var bandId = await harness.ExecuteDbAsync(db => db.PackageBands.Select(x => x.Id).SingleAsync());

        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(otherAgentId));

        var response = await harness.Client.PostAsJsonAsync("/agent/campaigns/prospects", new
        {
            fullName = "Duplicate Lead",
            email = "duplicate.lead@example.com",
            phone = "082 123 4567",
            packageBandId = bandId,
            campaignName = "Attempted duplicate"
        });
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, content);
        content.Should().Contain("A prospect with this contact information already exists.");
    }

    [Fact]
    public async Task AgentCanCreateAwaitingPurchaseCampaignForRegisteredClientOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                clientUser.Email = "beggie38.bali@gmail.com";
                clientUser.FullName = "Beggie Bali";

                var agentUser = TestSeed.CreateAgent();
                agentUser.Email = "thabo.agent@advertified.test";
                agentUser.FullName = "Thabo Agent";

                var band = new PackageBand
                {
                    Id = Guid.NewGuid(),
                    Code = "scale",
                    Name = "Scale",
                    MinBudget = 150000m,
                    MaxBudget = 500000m,
                    SortOrder = 3,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                db.UserAccounts.AddRange(clientUser, agentUser);
                db.PackageBands.Add(band);
                db.SaveChanges();
            });

        var agentUserId = await harness.ExecuteDbAsync(db =>
            db.UserAccounts
                .Where(x => x.Role == UserRole.Agent && x.Email == "thabo.agent@advertified.test")
                .Select(x => x.Id)
                .SingleAsync());
        var bandId = await harness.ExecuteDbAsync(db => db.PackageBands.Select(x => x.Id).SingleAsync());

        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(agentUserId));

        var response = await harness.Client.PostAsJsonAsync("/agent/campaigns/registered-prospects", new
        {
            email = "beggie38.bali@gmail.com",
            packageBandId = bandId,
            campaignName = "Test prospect campaign"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<CampaignDetailResponse>();
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("awaiting_purchase");
        payload.AiUnlocked.Should().BeFalse();
        payload.ClientEmail.Should().Be("beggie38.bali@gmail.com");
    }

    [Fact]
    public async Task PublicProspectQuestionnaire_SubmissionUnlocksAiAfterBriefSubmissionOverHttp()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var band = new PackageBand
                {
                    Id = Guid.NewGuid(),
                    Code = "scale",
                    Name = "Scale",
                    MinBudget = 150000m,
                    MaxBudget = 500000m,
                    SortOrder = 3,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                db.PackageBands.Add(band);
                db.SaveChanges();
            },
            configureServices: services =>
            {
                services.AddSingleton<StubTemplatedEmailService>();
                services.AddSingleton<ITemplatedEmailService>(sp => sp.GetRequiredService<StubTemplatedEmailService>());
            });

        var bandId = await harness.ExecuteDbAsync(db => db.PackageBands.Select(x => x.Id).SingleAsync());

        var response = await harness.Client.PostAsJsonAsync("/public/prospect-questionnaires", new
        {
            fullName = "Public Prospect",
            email = "public.prospect@example.com",
            phone = "0821234567",
            packageBandId = bandId,
            campaignName = "Public questionnaire campaign",
            brief = new
            {
                objective = "launch",
                geographyScope = "regional",
                provinces = new[] { "Gauteng" },
                preferredMediaTypes = new[] { "radio", "ooh" },
                openToUpsell = false,
                specialRequirements = "Retail launch campaign"
            }
        });
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        var campaign = await harness.ExecuteDbAsync(db => db.Campaigns.Include(x => x.CampaignBrief).SingleAsync());
        campaign.Status.Should().Be(CampaignStatuses.BriefSubmitted);
        campaign.AiUnlocked.Should().BeTrue();
        campaign.CampaignBrief.Should().NotBeNull();
        campaign.CampaignBrief!.SubmittedAt.Should().NotBeNull();
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

                var recommendations = new[]
                {
                    TestSeed.CreateRecommendation(campaign.Id, status: "draft", recommendationType: "hybrid:balanced"),
                    TestSeed.CreateRecommendation(campaign.Id, status: "draft", recommendationType: "hybrid:ooh_focus"),
                    TestSeed.CreateRecommendation(campaign.Id, status: "draft", recommendationType: "hybrid:radio_focus")
                };

                db.UserAccounts.AddRange(clientUser, agentUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.CampaignRecommendations.AddRange(recommendations);
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
    public async Task SendToClient_AttachesEachCurrentRecommendationPdfToEmail()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "planning_in_progress");
                campaign.AssignedAgentUserId = agentUser.Id;
                campaign.AssignedAt = DateTime.UtcNow;

                var recommendations = new[]
                {
                    TestSeed.CreateRecommendation(campaign.Id, status: "draft", recommendationType: "hybrid:balanced"),
                    TestSeed.CreateRecommendation(campaign.Id, status: "draft", recommendationType: "hybrid:ooh_focus"),
                    TestSeed.CreateRecommendation(campaign.Id, status: "draft", recommendationType: "hybrid:radio_focus")
                };

                db.UserAccounts.AddRange(clientUser, agentUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.CampaignRecommendations.AddRange(recommendations);
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

        var sendResponse = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/send-to-client", new
        {
            message = "Please compare the attached recommendation options."
        });

        sendResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var emailService = harness.Services.GetRequiredService<StubTemplatedEmailService>();
        emailService.SentEmails.Should().ContainSingle();

        var sentEmail = emailService.SentEmails[0];
        sentEmail.TemplateName.Should().Be("recommendation-ready");
        sentEmail.Attachments.Should().NotBeNull();
        sentEmail.Attachments!.Should().HaveCount(3);
        sentEmail.Attachments.Select(x => x.FileName).Should().OnlyHaveUniqueItems();
        sentEmail.Attachments.Select(x => x.FileName).Should().Contain(new[]
        {
            $"advertified-recommendation-{campaignId:D}-balanced.pdf",
            $"advertified-recommendation-{campaignId:D}-ooh-focus.pdf",
            $"advertified-recommendation-{campaignId:D}-radio-focus.pdf"
        });
    }

    [Fact]
    public async Task PublicProposalApproval_SelectsRecommendationBeforeCheckoutWhenPaymentIsUnpaid()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "review_ready");
                order.PaymentStatus = "unpaid";
                order.PurchasedAt = null;
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
                ConfigureRealPackagePurchaseServices(services);
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
        var content = await approveResponse.Content.ReadAsStringAsync();
        using var json = System.Text.Json.JsonDocument.Parse(content);
        json.RootElement.GetProperty("status").GetString().Should().Be(RecommendationSelectionStatuses.PendingPayment);

        var selectedCampaign = await harness.ExecuteDbAsync(db => db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignRecommendations)
            .SingleAsync(x => x.Id == campaignId));
        selectedCampaign.Status.Should().Be("review_ready");
        selectedCampaign.PackageOrder.Should().NotBeNull();
        selectedCampaign.PackageOrder!.SelectedRecommendationId.Should().Be(recommendationId);
        selectedCampaign.PackageOrder.SelectionStatus.Should().Be(RecommendationSelectionStatuses.PendingPayment);
        selectedCampaign.PackageOrder.SelectionSource.Should().Be("public_proposal");
        selectedCampaign.CampaignRecommendations.Should().Contain(x => x.Status == "sent_to_client");

        var emailService = harness.Services.GetRequiredService<StubTemplatedEmailService>();
        emailService.SentEmails.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicProposalPayment_AutoApprovesSelectedRecommendationAfterPayment()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "review_ready");
                order.PaymentStatus = "unpaid";
                order.PurchasedAt = null;
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
                ConfigureRealPackagePurchaseServices(services);
                services.AddSingleton<StubTemplatedEmailService>();
                services.AddSingleton<ITemplatedEmailService>(sp => sp.GetRequiredService<StubTemplatedEmailService>());
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var orderId = await harness.ExecuteDbAsync(db => db.PackageOrders.Select(x => x.Id).SingleAsync());
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

        var paymentResponse = await harness.Client.PostAsJsonAsync("/payments/webhook", new
        {
            packageOrderId = orderId,
            paymentStatus = "paid",
            paymentReference = "test-payment-reference"
        });
        var paymentContent = await paymentResponse.Content.ReadAsStringAsync();
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.Accepted, paymentContent);

        var paidCampaign = await harness.ExecuteDbAsync(db => db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignRecommendations)
            .SingleAsync(x => x.Id == campaignId));
        paidCampaign.Status.Should().Be("approved");
        paidCampaign.PackageOrder.PaymentStatus.Should().Be("paid");
        paidCampaign.PackageOrder.TermsAcceptedAt.Should().NotBeNull();
        paidCampaign.PackageOrder.TermsVersion.Should().Be(TermsAcceptancePolicy.CurrentVersion);
        paidCampaign.PackageOrder.TermsAcceptanceSource.Should().Be("payment");
        paidCampaign.PackageOrder.SelectedRecommendationId.Should().Be(recommendationId);
        paidCampaign.PackageOrder.SelectionStatus.Should().Be(RecommendationSelectionStatuses.AutoApproved);
        paidCampaign.CampaignRecommendations.Should().Contain(x => x.Id == recommendationId && x.Status == "approved");

        var emailService = harness.Services.GetRequiredService<StubTemplatedEmailService>();
        emailService.SentEmails.Select(x => x.TemplateName).Should().Contain(new[]
        {
            "recommendation-approved",
            "activation-in-progress",
            "creative-queue-update"
        });
    }

    private static void ConfigureRealPackagePurchaseServices(IServiceCollection services)
    {
        services.AddScoped<IVodaPayCheckoutService, StubVodaPayCheckoutService>();
        services.AddScoped<IPaymentStateCache, StubPaymentStateCache>();
        services.AddScoped<IPaymentAuditService, StubPaymentAuditService>();
        services.AddScoped<IWebhookQueueService, StubWebhookQueueService>();
        services.AddScoped<IPricingSettingsProvider, PricingSettingsProvider>();
        services.AddScoped<IPackagePurchaseService, PackagePurchaseService>();
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
        var responseContent = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseContent);

        var emailService = harness.Services.GetRequiredService<StubTemplatedEmailService>();
        emailService.SentEmails.Select(x => x.TemplateName).Should().Contain("agent-working");
        emailService.SentEmails.Select(x => x.TemplateName).Should().NotContain("recommendation-preparing");

        var savedCampaign = await harness.ExecuteDbAsync(db => db.Campaigns.Include(x => x.CampaignRecommendations).SingleAsync(x => x.Id == campaignId));
        savedCampaign.CampaignRecommendations.Should().NotBeEmpty();
        savedCampaign.CampaignRecommendations.Should().OnlyContain(x =>
            !string.IsNullOrWhiteSpace(x.ClientExplanation)
            && x.MarginStatus == "low_margin_review"
            && x.SupplierAvailabilityStatus == "unconfirmed");
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
    public async Task AgentGenerateRecommendation_WithExplicitTargetMix_StillCreatesThreeProposalVariants()
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
                    CampaignName = "Explicit target mix campaign",
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
                        PreferredMediaTypesJson = "[\"radio\",\"ooh\",\"digital\"]",
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

        var response = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/generate-recommendation", new
        {
            targetRadioShare = 20,
            targetOohShare = 30,
            targetTvShare = 0,
            targetDigitalShare = 50
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recommendations = await harness.ExecuteDbAsync(db => db.CampaignRecommendations
            .Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.RecommendationType)
            .ToListAsync());

        recommendations.Should().HaveCount(3);
        recommendations.Should().Contain(new[]
        {
            "hybrid:balanced",
            "hybrid:ooh_focus",
            "hybrid:radio_focus"
        });
        recommendations.Should().NotContain(type => type.EndsWith(":requested_mix"));
    }

    [Fact]
    public async Task AgentCanCreateDraftRecommendation_BeforePayment()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var now = DateTime.UtcNow;
                var clientUser = TestSeed.CreateUser();
                var agentUser = TestSeed.CreateAgent();
                var (band, order, campaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "awaiting_purchase");
                order.PaymentStatus = "unpaid";
                order.PurchasedAt = null;
                campaign.AssignedAgentUserId = agentUser.Id;
                campaign.AssignedAt = now;
                campaign.UpdatedAt = now;

                db.UserAccounts.AddRange(clientUser, agentUser);
                db.PackageBands.Add(band);
                db.PackageOrders.Add(order);
                db.Campaigns.Add(campaign);
                db.SaveChanges();
            });

        var campaignId = await harness.ExecuteDbAsync(db => db.Campaigns.Select(x => x.Id).SingleAsync());
        var agentUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Agent).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(agentUserId));

        var response = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/recommendations", new
        {
            notes = "Manual recommendation draft",
            inventoryItems = new[]
            {
                new
                {
                    id = "inv-1",
                    type = "ooh",
                    station = "Sandton Digital Billboard",
                    region = "Gauteng",
                    language = "English",
                    showDaypart = "N/A",
                    timeBand = "N/A",
                    slotType = "N/A",
                    duration = "N/A",
                    rate = 250000m,
                    restrictions = "None",
                    quantity = 1
                }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var saved = await harness.ExecuteDbAsync(db => db.CampaignRecommendations
            .Include(x => x.RecommendationItems)
            .SingleAsync(x => x.CampaignId == campaignId));

        saved.Status.Should().Be("draft");
        saved.RevisionNumber.Should().Be(1);
        saved.TotalCost.Should().Be(250000m);
        saved.RecommendationItems.Should().HaveCount(1);
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

        var bookingResponse = await harness.Client.PostAsJsonAsync($"/agent/campaigns/{campaignId}/supplier-bookings", new
        {
            supplierOrStation = "Metro FM",
            channel = "radio",
            bookingStatus = "booked",
            committedAmount = 45000m,
            liveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            liveTo = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(13)),
            availabilityStatus = "confirmed",
            supplierConfirmationReference = "METRO-BOOK-001",
            confirmedAt = DateTimeOffset.UtcNow,
            notes = "Confirmed for launch burst"
        });

        var bookingContent = await bookingResponse.Content.ReadAsStringAsync();
        bookingResponse.StatusCode.Should().Be(HttpStatusCode.Accepted, bookingContent);

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

        campaign.CampaignSupplierBookings.Should().ContainSingle(x =>
            x.SupplierOrStation == "Metro FM"
            && x.BookingStatus == "booked"
            && x.AvailabilityStatus == "confirmed"
            && x.SupplierConfirmationReference == "METRO-BOOK-001"
            && x.ConfirmedAt.HasValue);
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
    public async Task AdminCampaignOperations_SupportsPagingSortAndAttentionFilter()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var now = DateTime.UtcNow;
                var clientUser = TestSeed.CreateUser();
                var adminUser = TestSeed.CreateAdmin();
                var (scaleBand, scaleOrder, attentionCampaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 250000m, status: "launched");
                var (dominanceBand, dominanceOrder, healthyCampaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 550000m, status: "launched");

                attentionCampaign.CampaignName = "Attention campaign";
                healthyCampaign.CampaignName = "Healthy campaign";
                healthyCampaign.PausedAt = now;
                healthyCampaign.PauseReason = "Ops hold";

                var attentionBooking = new CampaignSupplierBooking
                {
                    Id = Guid.NewGuid(),
                    CampaignId = attentionCampaign.Id,
                    SupplierOrStation = "Station A",
                    Channel = "radio",
                    BookingStatus = "live",
                    CommittedAmount = 100000m,
                    LiveFrom = DateOnly.FromDateTime(now.Date.AddDays(-3)),
                    LiveTo = DateOnly.FromDateTime(now.Date.AddDays(10)),
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var healthyBooking = new CampaignSupplierBooking
                {
                    Id = Guid.NewGuid(),
                    CampaignId = healthyCampaign.Id,
                    SupplierOrStation = "Station B",
                    Channel = "radio",
                    BookingStatus = "live",
                    CommittedAmount = 250000m,
                    LiveFrom = DateOnly.FromDateTime(now.Date.AddDays(-5)),
                    LiveTo = DateOnly.FromDateTime(now.Date.AddDays(7)),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                var attentionReport = new CampaignDeliveryReport
                {
                    Id = Guid.NewGuid(),
                    CampaignId = attentionCampaign.Id,
                    SupplierBookingId = attentionBooking.Id,
                    ReportType = "delivery_update",
                    Headline = "Under-delivering",
                    ReportedAt = now.AddDays(-1),
                    Impressions = 12000,
                    PlaysOrSpots = 40,
                    SpendDelivered = 20000m,
                    CreatedAt = now
                };
                var healthyReport = new CampaignDeliveryReport
                {
                    Id = Guid.NewGuid(),
                    CampaignId = healthyCampaign.Id,
                    SupplierBookingId = healthyBooking.Id,
                    ReportType = "delivery_update",
                    Headline = "On track",
                    ReportedAt = now.AddDays(-1),
                    Impressions = 180000,
                    PlaysOrSpots = 450,
                    SpendDelivered = 225000m,
                    CreatedAt = now
                };

                attentionCampaign.CampaignBrief = new CampaignBrief
                {
                    Id = Guid.NewGuid(),
                    CampaignId = attentionCampaign.Id,
                    Objective = "launch",
                    GeographyScope = "regional",
                    StartDate = DateOnly.FromDateTime(now.Date.AddDays(-7)),
                    EndDate = DateOnly.FromDateTime(now.Date.AddDays(14)),
                    CreatedAt = now,
                    UpdatedAt = now
                };
                healthyCampaign.CampaignBrief = new CampaignBrief
                {
                    Id = Guid.NewGuid(),
                    CampaignId = healthyCampaign.Id,
                    Objective = "launch",
                    GeographyScope = "regional",
                    StartDate = DateOnly.FromDateTime(now.Date.AddDays(-7)),
                    EndDate = DateOnly.FromDateTime(now.Date.AddDays(14)),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                db.UserAccounts.AddRange(clientUser, adminUser);
                db.PackageBands.AddRange(scaleBand, dominanceBand);
                db.PackageOrders.AddRange(scaleOrder, dominanceOrder);
                db.Campaigns.AddRange(attentionCampaign, healthyCampaign);
                db.CampaignSupplierBookings.AddRange(attentionBooking, healthyBooking);
                db.CampaignDeliveryReports.AddRange(attentionReport, healthyReport);
                db.SaveChanges();
            });

        var adminUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Admin).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var firstPageResponse = await harness.Client.GetAsync("/admin/campaign-operations?page=1&pageSize=1&sortBy=highest_spend&attentionOnly=false");
        firstPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstPagePayload = await firstPageResponse.Content.ReadFromJsonAsync<AdminCampaignOperationsResponse>();
        firstPagePayload.Should().NotBeNull();
        firstPagePayload!.Page.Should().Be(1);
        firstPagePayload.PageSize.Should().Be(10);
        firstPagePayload.SortBy.Should().Be("highest_spend");
        firstPagePayload.AttentionOnly.Should().BeFalse();
        firstPagePayload.TotalCount.Should().Be(2);
        firstPagePayload.TotalPages.Should().Be(1);
        firstPagePayload.HasPreviousPage.Should().BeFalse();
        firstPagePayload.HasNextPage.Should().BeFalse();
        firstPagePayload.PerformanceAttentionThresholdPercent.Should().Be(60);
        firstPagePayload.TotalPausedCount.Should().Be(1);
        firstPagePayload.TotalScheduledCount.Should().Be(2);
        firstPagePayload.TotalPerformanceAttentionCount.Should().Be(1);
        firstPagePayload.Items.Should().HaveCount(2);
        firstPagePayload.Items[0].CampaignName.Should().Be("Healthy campaign");

        var attentionResponse = await harness.Client.GetAsync("/admin/campaign-operations?page=1&pageSize=10&sortBy=highest_spend&attentionOnly=true");
        attentionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var attentionPayload = await attentionResponse.Content.ReadFromJsonAsync<AdminCampaignOperationsResponse>();
        attentionPayload.Should().NotBeNull();
        attentionPayload!.AttentionOnly.Should().BeTrue();
        attentionPayload.TotalCount.Should().Be(1);
        attentionPayload.TotalPages.Should().Be(1);
        attentionPayload.HasPreviousPage.Should().BeFalse();
        attentionPayload.HasNextPage.Should().BeFalse();
        attentionPayload.Items.Should().ContainSingle();
        attentionPayload.Items[0].CampaignName.Should().Be("Attention campaign");
        attentionPayload.Items[0].PerformanceDeliveryPercent.Should().BeLessThan(60);
    }

    [Fact]
    public async Task AdminCampaignOperations_DeliveryRiskSort_PrioritizesNoReportAndLowDelivery()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var now = DateTime.UtcNow;
                var clientUser = TestSeed.CreateUser();
                var adminUser = TestSeed.CreateAdmin();
                var (scaleBand, scaleOrder, noReportCampaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 180000m, status: "launched");
                var (dominanceBand, dominanceOrder, lowDeliveryCampaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 550000m, status: "launched");
                var (boostBand, boostOrder, healthyCampaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 300000m, status: "launched");

                noReportCampaign.CampaignName = "No report campaign";
                lowDeliveryCampaign.CampaignName = "Low delivery campaign";
                healthyCampaign.CampaignName = "Healthy campaign";

                var noReportBooking = new CampaignSupplierBooking
                {
                    Id = Guid.NewGuid(),
                    CampaignId = noReportCampaign.Id,
                    SupplierOrStation = "Station NR",
                    Channel = "radio",
                    BookingStatus = "live",
                    CommittedAmount = 180000m,
                    LiveFrom = DateOnly.FromDateTime(now.Date.AddDays(-2)),
                    LiveTo = DateOnly.FromDateTime(now.Date.AddDays(10)),
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var lowDeliveryBooking = new CampaignSupplierBooking
                {
                    Id = Guid.NewGuid(),
                    CampaignId = lowDeliveryCampaign.Id,
                    SupplierOrStation = "Station LD",
                    Channel = "radio",
                    BookingStatus = "live",
                    CommittedAmount = 200000m,
                    LiveFrom = DateOnly.FromDateTime(now.Date.AddDays(-2)),
                    LiveTo = DateOnly.FromDateTime(now.Date.AddDays(10)),
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var healthyBooking = new CampaignSupplierBooking
                {
                    Id = Guid.NewGuid(),
                    CampaignId = healthyCampaign.Id,
                    SupplierOrStation = "Station H",
                    Channel = "radio",
                    BookingStatus = "live",
                    CommittedAmount = 200000m,
                    LiveFrom = DateOnly.FromDateTime(now.Date.AddDays(-2)),
                    LiveTo = DateOnly.FromDateTime(now.Date.AddDays(10)),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                var lowDeliveryReport = new CampaignDeliveryReport
                {
                    Id = Guid.NewGuid(),
                    CampaignId = lowDeliveryCampaign.Id,
                    SupplierBookingId = lowDeliveryBooking.Id,
                    ReportType = "delivery_update",
                    Headline = "Low delivery",
                    ReportedAt = now.AddDays(-1),
                    Impressions = 15000,
                    PlaysOrSpots = 30,
                    SpendDelivered = 40000m,
                    CreatedAt = now
                };
                var healthyReport = new CampaignDeliveryReport
                {
                    Id = Guid.NewGuid(),
                    CampaignId = healthyCampaign.Id,
                    SupplierBookingId = healthyBooking.Id,
                    ReportType = "delivery_update",
                    Headline = "Healthy delivery",
                    ReportedAt = now.AddDays(-1),
                    Impressions = 150000,
                    PlaysOrSpots = 320,
                    SpendDelivered = 180000m,
                    CreatedAt = now
                };

                db.UserAccounts.AddRange(clientUser, adminUser);
                db.PackageBands.AddRange(scaleBand, dominanceBand, boostBand);
                db.PackageOrders.AddRange(scaleOrder, dominanceOrder, boostOrder);
                db.Campaigns.AddRange(noReportCampaign, lowDeliveryCampaign, healthyCampaign);
                db.CampaignSupplierBookings.AddRange(noReportBooking, lowDeliveryBooking, healthyBooking);
                db.CampaignDeliveryReports.AddRange(lowDeliveryReport, healthyReport);
                db.SaveChanges();
            });

        var adminUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Admin).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var riskResponse = await harness.Client.GetAsync("/admin/campaign-operations?page=1&pageSize=25&sortBy=delivery_risk&attentionOnly=false");
        riskResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var riskPayload = await riskResponse.Content.ReadFromJsonAsync<AdminCampaignOperationsResponse>();
        riskPayload.Should().NotBeNull();
        riskPayload!.Items.Should().HaveCount(3);
        riskPayload.TotalPages.Should().Be(1);
        riskPayload.HasPreviousPage.Should().BeFalse();
        riskPayload.HasNextPage.Should().BeFalse();
        riskPayload.PerformanceAttentionThresholdPercent.Should().Be(60);
        riskPayload.TotalPerformanceAttentionCount.Should().Be(2);
        riskPayload.Items[0].CampaignName.Should().Be("No report campaign");
        riskPayload.Items[1].CampaignName.Should().Be("Low delivery campaign");
        riskPayload.Items[2].CampaignName.Should().Be("Healthy campaign");

        var attentionOnlyResponse = await harness.Client.GetAsync("/admin/campaign-operations?page=1&pageSize=25&sortBy=delivery_risk&attentionOnly=true");
        attentionOnlyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var attentionOnlyPayload = await attentionOnlyResponse.Content.ReadFromJsonAsync<AdminCampaignOperationsResponse>();
        attentionOnlyPayload.Should().NotBeNull();
        attentionOnlyPayload!.Items.Should().HaveCount(2);
        attentionOnlyPayload.TotalPages.Should().Be(1);
        attentionOnlyPayload.HasPreviousPage.Should().BeFalse();
        attentionOnlyPayload.HasNextPage.Should().BeFalse();
        attentionOnlyPayload.Items.Select(x => x.CampaignName).Should().Contain(new[] { "No report campaign", "Low delivery campaign" });
    }

    [Fact]
    public async Task AdminCampaignOperations_NormalizesInvalidPagingAndSortInputs()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var adminUser = TestSeed.CreateAdmin();
                var (scaleBand, scaleOrder, firstCampaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 180000m, status: "launched");
                var (boostBand, boostOrder, secondCampaign) = TestSeed.CreateCampaignGraph(clientUser, selectedBudget: 280000m, status: "launched");

                firstCampaign.CampaignName = "First campaign";
                secondCampaign.CampaignName = "Second campaign";

                db.UserAccounts.AddRange(clientUser, adminUser);
                db.PackageBands.AddRange(scaleBand, boostBand);
                db.PackageOrders.AddRange(scaleOrder, boostOrder);
                db.Campaigns.AddRange(firstCampaign, secondCampaign);
                db.SaveChanges();
            });

        var adminUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Admin).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var response = await harness.Client.GetAsync("/admin/campaign-operations?page=999&pageSize=1000&sortBy=invalid_mode&attentionOnly=false");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AdminCampaignOperationsResponse>();

        payload.Should().NotBeNull();
        payload!.Page.Should().Be(1);
        payload.PageSize.Should().Be(100);
        payload.TotalCount.Should().Be(2);
        payload.TotalPages.Should().Be(1);
        payload.HasPreviousPage.Should().BeFalse();
        payload.HasNextPage.Should().BeFalse();
        payload.SortBy.Should().Be("delivery_risk");
        payload.AttentionOnly.Should().BeFalse();
        payload.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task AdminCampaignOperations_ReturnsCorrectPagingFlagsAcrossMultiplePages()
    {
        await using var harness = await TestApiHarness.CreateAsync(
            seed: db =>
            {
                var clientUser = TestSeed.CreateUser();
                var adminUser = TestSeed.CreateAdmin();
                db.UserAccounts.AddRange(clientUser, adminUser);

                for (var index = 0; index < 12; index++)
                {
                    var (band, order, campaign) = TestSeed.CreateCampaignGraph(
                        clientUser,
                        selectedBudget: 180000m + (index * 1000m),
                        status: "launched");
                    campaign.CampaignName = $"Campaign {index + 1:D2}";
                    db.PackageBands.Add(band);
                    db.PackageOrders.Add(order);
                    db.Campaigns.Add(campaign);
                }

                db.SaveChanges();
            });

        var adminUserId = await harness.ExecuteDbAsync(db => db.UserAccounts.Where(x => x.Role == UserRole.Admin).Select(x => x.Id).SingleAsync());
        harness.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await harness.CreateSessionTokenAsync(adminUserId));

        var firstPageResponse = await harness.Client.GetAsync("/admin/campaign-operations?page=1&pageSize=10&sortBy=campaign_name&attentionOnly=false");
        firstPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstPagePayload = await firstPageResponse.Content.ReadFromJsonAsync<AdminCampaignOperationsResponse>();
        firstPagePayload.Should().NotBeNull();
        firstPagePayload!.Page.Should().Be(1);
        firstPagePayload.PageSize.Should().Be(10);
        firstPagePayload.TotalCount.Should().Be(12);
        firstPagePayload.TotalPages.Should().Be(2);
        firstPagePayload.HasPreviousPage.Should().BeFalse();
        firstPagePayload.HasNextPage.Should().BeTrue();
        firstPagePayload.Items.Should().HaveCount(10);

        var secondPageResponse = await harness.Client.GetAsync("/admin/campaign-operations?page=2&pageSize=10&sortBy=campaign_name&attentionOnly=false");
        secondPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondPagePayload = await secondPageResponse.Content.ReadFromJsonAsync<AdminCampaignOperationsResponse>();
        secondPagePayload.Should().NotBeNull();
        secondPagePayload!.Page.Should().Be(2);
        secondPagePayload.PageSize.Should().Be(10);
        secondPagePayload.TotalCount.Should().Be(12);
        secondPagePayload.TotalPages.Should().Be(2);
        secondPagePayload.HasPreviousPage.Should().BeTrue();
        secondPagePayload.HasNextPage.Should().BeFalse();
        secondPagePayload.Items.Should().HaveCount(2);
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
                  db,
                  Options.Create(new ResendOptions
                  {
                      ApiKey = string.Empty,
                      AllowLocalArchiveFallback = true,
                      LocalArchiveDirectory = "outbox",
                    SenderAddresses = new Dictionary<string, string>
                      {
                          ["noreply"] = "Advertified <noreply@advertified.com>"
                      }
                  }),
                  new EmailDeliveryTrackingService(db, new PassthroughEmailIntegrationSecretCipher(), NullLogger<EmailDeliveryTrackingService>.Instance));

              await service.SendAsync(
                  "test-template",
                  "client@example.com",
                "noreply",
                new Dictionary<string, string?> { ["Name"] = "Brian" },
                null,
                  null,
                  CancellationToken.None);

              var queuedMessage = await db.EmailDeliveryMessages.SingleAsync();
              queuedMessage.Status.Should().Be("pending");
              queuedMessage.BodyHtml.Should().Contain("Hello Brian");

              var transport = new ResendEmailTransport(
                  new HttpClient { BaseAddress = new Uri("https://api.resend.com/") },
                  Options.Create(new ResendOptions
                  {
                      ApiKey = string.Empty,
                      AllowLocalArchiveFallback = true,
                      LocalArchiveDirectory = "outbox",
                      SenderAddresses = new Dictionary<string, string>
                      {
                          ["noreply"] = "Advertified <noreply@advertified.com>"
                      }
                  }),
                  new StubWebHostEnvironment(archiveRoot));
              var dispatcher = new ResendEmailOutboxDispatcher(
                  db,
                  new EmailDeliveryTrackingService(db, new PassthroughEmailIntegrationSecretCipher(), NullLogger<EmailDeliveryTrackingService>.Instance),
                  transport,
                  Options.Create(new ResendOptions
                  {
                      ApiKey = string.Empty,
                      AllowLocalArchiveFallback = true,
                      LocalArchiveDirectory = "outbox",
                      SenderAddresses = new Dictionary<string, string>
                      {
                          ["noreply"] = "Advertified <noreply@advertified.com>"
                      }
                  }),
                  NullLogger<ResendEmailOutboxDispatcher>.Instance);

              (await dispatcher.DispatchPendingAsync(CancellationToken.None)).Should().Be(1);

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

public class ResendStartupValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ThrowsWhenApiKeyIsMissingAndFallbackIsDisabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<ResendOptions>(options =>
        {
            options.ApiKey = string.Empty;
            options.BaseUrl = "https://api.resend.com";
            options.AllowLocalArchiveFallback = false;
            options.SenderAddresses["noreply"] = "Advertified <noreply@advertified.com>";
        });

        await using var provider = services.BuildServiceProvider();

        await FluentActions
            .Invoking(() => ResendStartupValidator.ValidateAsync(provider))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Resend:ApiKey is missing*");
    }

    [Fact]
    public async Task ValidateAsync_AllowsArchiveFallbackWhenExplicitlyEnabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<ResendOptions>(options =>
        {
            options.ApiKey = string.Empty;
            options.BaseUrl = "https://api.resend.com";
            options.AllowLocalArchiveFallback = true;
            options.SenderAddresses["noreply"] = "Advertified <noreply@advertified.com>";
        });

        await using var provider = services.BuildServiceProvider();

        await FluentActions
            .Invoking(() => ResendStartupValidator.ValidateAsync(provider))
            .Should()
            .NotThrowAsync();
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
    public Task UpdatePricingSettingsAsync(UpdateAdminPricingSettingsRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task UpdateEnginePolicyAsync(string packageCode, UpdateAdminEnginePolicyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task UpdatePlanningAllocationSettingsAsync(UpdateAdminPlanningAllocationSettingsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
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
        return Task.FromResult(new CampaignAdMetricsSummary(campaignId, 0, 0, 0, 0, 0, 0m, 0m, 0m, null, null, null, null, null));
    }

    public Task<SyncCampaignMetricsResult> SyncCampaignMetricsAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        SyncCallCount++;
        return Task.FromResult(new SyncCampaignMetricsResult(campaignId, 0, new CampaignAdMetricsSummary(campaignId, 0, 0, 0, 0, 0, 0m, 0m, 0m, null, null, null, null, null)));
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

internal sealed class StubAdPlatformAccessTokenService : IAdPlatformAccessTokenService
{
    public Task<string?> ResolveAccessTokenAsync(
        CampaignAdPlatformLink? link,
        string platform,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>("test-access-token");
    }
}

internal sealed class StubAdPlatformConnectionService : IAdPlatformConnectionService
{
    public Task<IReadOnlyList<CampaignAdPlatformConnectionResponse>> GetCampaignConnectionsAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CampaignAdPlatformConnectionResponse> result = Array.Empty<CampaignAdPlatformConnectionResponse>();
        return Task.FromResult(result);
    }

    public Task<CampaignAdPlatformConnectionResponse> UpsertCampaignConnectionAsync(
        Guid campaignId,
        Guid? ownerUserId,
        UpsertCampaignAdPlatformConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var response = new CampaignAdPlatformConnectionResponse
        {
            LinkId = Guid.NewGuid(),
            ConnectionId = Guid.NewGuid(),
            CampaignId = campaignId,
            Provider = request.Provider?.Trim() ?? "Meta",
            ExternalAccountId = request.ExternalAccountId?.Trim() ?? string.Empty,
            AccountName = request.AccountName?.Trim() ?? "Test Account",
            ExternalCampaignId = request.ExternalCampaignId?.Trim(),
            IsPrimary = request.IsPrimary,
            Status = request.Status?.Trim() ?? "active",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(response);
    }
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

internal sealed class StubLocationCatalogService : ILocationCatalogService
{
    public Task SeedResolvedLocationAsync(SaveCampaignBriefRequest request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

internal sealed class StubCampaignPlanningTargetResolver : ICampaignPlanningTargetResolver
{
    public CampaignPlanningTargetResolution Resolve(CampaignBrief? brief) => new();

    public CampaignPlanningTargetResolution Resolve(CampaignPlanningRequest request) => new();
}

internal sealed class StubCampaignBusinessLocationResolver : ICampaignBusinessLocationResolver
{
    public CampaignBusinessLocationResolution Resolve(Campaign campaign) => new();
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
        builder.Services.AddMemoryCache();
        builder.Services.AddControllers().AddApplicationPart(typeof(Advertified.App.Controllers.CampaignsController).Assembly);
        builder.Services.Configure<FrontendOptions>(options => options.BaseUrl = "http://localhost:5173");
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        builder.Services.AddScoped<ISessionTokenService, SessionTokenService>();
        builder.Services.AddScoped<IProposalAccessTokenService, ProposalAccessTokenService>();
        builder.Services.AddScoped<IPasswordHashingService, PasswordHashingService>();
        builder.Services.AddScoped<IChangeAuditService, ChangeAuditService>();
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "AdvertifiedSession";
            options.DefaultChallengeScheme = "AdvertifiedSession";
        })
        .AddScheme<AuthenticationSchemeOptions, SessionTokenAuthenticationHandler>("AdvertifiedSession", options => { });
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        builder.Services.AddScoped<IAdminDashboardService, StubAdminDashboardService>();
        builder.Services.AddScoped<IAdminMutationService, StubAdminMutationService>();
        builder.Services.AddScoped<ICampaignAccessService, CampaignAccessService>();
        builder.Services.AddScoped<IAgentCampaignOwnershipService, AgentCampaignOwnershipService>();
        builder.Services.AddScoped<IAgentAreaRoutingService, StubAgentAreaRoutingService>();
        builder.Services.AddScoped<ILocationCatalogService, StubLocationCatalogService>();
        builder.Services.AddScoped<ICampaignPlanningTargetResolver, StubCampaignPlanningTargetResolver>();
        builder.Services.AddScoped<ICampaignBusinessLocationResolver, StubCampaignBusinessLocationResolver>();
        builder.Services.AddScoped<FormOptionsService>();
        builder.Services.AddScoped<ICampaignBriefService, CampaignBriefService>();
        builder.Services.AddScoped<CampaignPlanningRequestValidator>();
        builder.Services.AddScoped<RegisterRequestValidator>();
        builder.Services.AddScoped<SaveCampaignBriefRequestValidator>();
        builder.Services.AddScoped<IEmailVerificationService, StubEmailVerificationService>();
        builder.Services.AddScoped<IInvoiceService, StubInvoiceService>();
        builder.Services.AddScoped<IPackagePurchaseService, StubPackagePurchaseService>();
        builder.Services.AddScoped<IProspectDispositionService, ProspectDispositionService>();
        builder.Services.AddScoped<IProspectLeadLinkingService, ProspectLeadLinkingService>();
        builder.Services.AddScoped<IProspectLeadRegistrationService, ProspectLeadRegistrationService>();
        builder.Services.AddScoped<IRegistrationService, RegistrationService>();
        builder.Services.AddScoped<IPrivateDocumentStorage, StubPrivateDocumentStorage>();
        builder.Services.AddScoped<IRecommendationDocumentService, StubRecommendationDocumentService>();
        builder.Services.AddScoped<IRecommendationApprovalWorkflowService, RecommendationApprovalWorkflowService>();
        builder.Services.AddScoped<ICampaignStatusTransitionService, CampaignStatusTransitionService>();
        builder.Services.AddScoped<ICampaignExecutionTaskService, CampaignExecutionTaskService>();
        builder.Services.AddScoped<IPublicAssetStorage, StubPublicAssetStorage>();
        builder.Services.AddScoped<IMediaPlanningEngine, StubMediaPlanningEngine>();
        builder.Services.AddScoped<ICampaignReasoningService, StubCampaignReasoningService>();
        builder.Services.AddScoped(_ => new PlanningPolicySnapshotProvider(new PlanningPolicyOptions()));
        builder.Services.AddScoped<IPlanningPolicyService, PlanningPolicyService>();
        builder.Services.AddScoped<IPlanningBudgetAllocationService, StubPlanningBudgetAllocationService>();
        builder.Services.AddScoped<IPlanningRequestFactory, PlanningRequestFactory>();
        builder.Services.AddScoped<ICampaignRecommendationService, CampaignRecommendationService>();
        builder.Services.AddScoped<IAgentCampaignBookingOrchestrationService, AgentCampaignBookingOrchestrationService>();
        builder.Services.AddScoped<IAdPlatformAccessTokenService, StubAdPlatformAccessTokenService>();
        builder.Services.AddScoped<IAdPlatformConnectionService, StubAdPlatformConnectionService>();
        builder.Services.AddScoped<ILeadProposalConfidenceGateService, StubLeadProposalConfidenceGateService>();
        builder.Services.AddScoped<ICampaignBriefInterpretationService, StubCampaignBriefInterpretationService>();
        builder.Services.AddScoped<ITemplatedEmailService, StubTemplatedEmailService>();
        builder.Services.AddScoped<ICreativeCampaignOrchestrator, StubCreativeCampaignOrchestrator>();
        builder.Services.AddSingleton<IAssetJobQueue, StubAssetJobQueue>();

        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseMiddleware<Advertified.App.Middleware.ProblemDetailsExceptionHandlingMiddleware>();
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsGet(context.Request.Method)
                || HttpMethods.IsHead(context.Request.Method)
                || HttpMethods.IsOptions(context.Request.Method)
                || HttpMethods.IsTrace(context.Request.Method))
            {
                await next();
                return;
            }

            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                await next();
                return;
            }

            if (context.User?.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            if (!context.Request.Cookies.ContainsKey(SessionCookieDefaults.CookieName))
            {
                await next();
                return;
            }

            static string? ResolveOrigin(HttpContext httpContext)
            {
                if (httpContext.Request.Headers.TryGetValue("Origin", out var originValues))
                {
                    var origin = originValues.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(origin))
                    {
                        return origin.Trim().TrimEnd('/');
                    }
                }

                if (httpContext.Request.Headers.TryGetValue("Referer", out var refererValues))
                {
                    var referer = refererValues.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
                    {
                        return $"{refererUri.Scheme}://{refererUri.Authority}".TrimEnd('/');
                    }
                }

                return null;
            }

            var requestOrigin = ResolveOrigin(context);
            if (string.IsNullOrWhiteSpace(requestOrigin))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Title = "Cross-site request blocked.",
                    Detail = "Origin or Referer header is required for authenticated browser actions.",
                    Status = StatusCodes.Status403Forbidden
                });
                return;
            }

            if (!string.IsNullOrWhiteSpace(requestOrigin))
            {
                var requestHostOrigin = $"{context.Request.Scheme}://{context.Request.Host}".TrimEnd('/');
                var allowedOrigins = new[]
                {
                    "http://localhost:5173",
                    "https://localhost:5173",
                    "https://dev.advertified.com",
                    "http://dev.advertified.com",
                    "https://advertified.com",
                    "https://www.advertified.com",
                    requestHostOrigin
                };
                var allowed = allowedOrigins.Any(origin =>
                    string.Equals(origin.TrimEnd('/'), requestOrigin, StringComparison.OrdinalIgnoreCase));

                if (!allowed)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Title = "Cross-site request blocked.",
                        Detail = "The request origin is not allowed for authenticated browser actions.",
                        Status = StatusCodes.Status403Forbidden
                    });
                    return;
                }
            }

            await next();
        });
        app.UseAuthorization();
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

    public static CampaignRecommendation CreateRecommendation(Guid campaignId, string status, string recommendationType = "hybrid")
    {
        var now = DateTime.UtcNow;
        var recommendationId = Guid.NewGuid();
        return new CampaignRecommendation
        {
            Id = recommendationId,
            CampaignId = campaignId,
            RecommendationType = recommendationType,
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

internal sealed class StubLeadProposalConfidenceGateService : ILeadProposalConfidenceGateService
{
    public Task EnsureCampaignReadyAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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
    public List<SentEmailRecord> SentEmails { get; } = new();

    public Task SendAsync(
        string templateName,
        string recipientEmail,
        string senderKey,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlyCollection<EmailAttachment>? attachments,
        EmailTrackingContext? trackingContext,
        CancellationToken cancellationToken)
    {
        SentEmails.Add(new SentEmailRecord(
            templateName,
            recipientEmail,
            tokens,
            attachments?.ToArray()));
        return Task.CompletedTask;
    }
}

internal sealed record SentEmailRecord(
    string TemplateName,
    string RecipientEmail,
    IReadOnlyDictionary<string, string?> Tokens,
    IReadOnlyCollection<EmailAttachment>? Attachments);

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

internal sealed class StubPlanningBudgetAllocationService : IPlanningBudgetAllocationService
{
    public PlanningBudgetAllocation Resolve(CampaignPlanningRequest request)
    {
        return new PlanningBudgetAllocation
        {
            AudienceSegment = "test",
            ChannelPolicyKey = "test",
            GeoPolicyKey = "test",
            ChannelAllocations = new List<PlanningChannelAllocation>(),
            GeoAllocations = new List<PlanningGeoAllocation>()
        };
    }

    public PlanningBudgetAllocation RebalanceChannelTargets(CampaignPlanningRequest request, IReadOnlyDictionary<string, int> channelShares)
    {
        return Resolve(request);
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
    {
        return Task.FromResult(new Invoice
        {
            Id = Guid.NewGuid(),
            PackageOrderId = order.Id,
            CampaignId = order.Campaign?.Id,
            UserId = user.Id,
            CompanyId = businessProfile?.Id,
            InvoiceNumber = $"TEST-{order.Id:N}",
            Provider = order.PaymentProvider ?? "test",
            InvoiceType = invoiceType,
            Status = status,
            Currency = order.Currency,
            TotalAmount = order.Amount,
            CampaignName = order.Campaign?.CampaignName ?? band.Name,
            PackageName = band.Name,
            CustomerName = user.FullName,
            CustomerEmail = user.Email,
            CustomerAddress = string.Empty,
            CompanyName = businessProfile?.BusinessName ?? string.Empty,
            CompanyRegistrationNumber = businessProfile?.RegistrationNumber,
            CompanyVatNumber = businessProfile?.VatNumber,
            CompanyAddress = businessProfile is null
                ? null
                : $"{businessProfile.StreetAddress}, {businessProfile.City}, {businessProfile.Province}",
            PaymentReference = paymentReference,
            CreatedAtUtc = DateTime.UtcNow,
            DueAtUtc = dueAtUtc,
            PaidAtUtc = paidAtUtc
        });
    }

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

    public Task PrepareRecommendationCheckoutAsync(Guid campaignId, Guid recommendationId, string selectionSource, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task MarkOrderPaidAsync(Guid packageOrderId, string paymentReference, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task MarkOrderFailedAsync(Guid packageOrderId, string? paymentReference, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

internal sealed class StubVodaPayCheckoutService : IVodaPayCheckoutService
{
    public Task<VodaPayCheckoutSession> InitiateAsync(
        PackageOrder order,
        PackageBand band,
        UserAccount user,
        BusinessProfile? businessProfile,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new VodaPayCheckoutSession
        {
            SessionId = $"test-session-{order.Id:D}",
            CheckoutUrl = $"https://checkout.example.test/{order.Id:D}",
            TraceId = "test-trace",
            EchoData = order.Id.ToString("D"),
            ProviderReference = $"test-provider-{order.Id:D}"
        });
    }
}

internal sealed class StubPaymentStateCache : IPaymentStateCache
{
    private readonly Dictionary<string, PaymentStateCacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public Task SetAsync(string paymentReference, PaymentStateCacheEntry entry, CancellationToken cancellationToken)
    {
        _entries[paymentReference] = entry;
        return Task.CompletedTask;
    }

    public Task<PaymentStateCacheEntry?> GetAsync(string paymentReference, CancellationToken cancellationToken)
    {
        _entries.TryGetValue(paymentReference, out var entry);
        return Task.FromResult(entry);
    }
}

internal sealed class StubPaymentAuditService : IPaymentAuditService
{
    public Task<Guid> CreateProviderRequestAsync(
        Guid? packageOrderId,
        string provider,
        string eventType,
        string requestUrl,
        string requestHeadersJson,
        string requestBodyJson,
        string? externalReference,
        CancellationToken cancellationToken)
        => Task.FromResult(Guid.NewGuid());

    public Task CompleteProviderRequestAsync(
        Guid requestAuditId,
        int? responseStatusCode,
        string? responseHeadersJson,
        string? responseBodyText,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<Guid> CreateWebhookAsync(
        Guid? packageOrderId,
        string provider,
        string webhookPath,
        string headersJson,
        string bodyJson,
        string processedStatus,
        string? processedMessage,
        CancellationToken cancellationToken)
        => Task.FromResult(Guid.NewGuid());

    public Task CompleteWebhookAsync(
        Guid webhookAuditId,
        string processedStatus,
        string? processedMessage,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}

internal sealed class StubWebhookQueueService : IWebhookQueueService
{
    public Task<bool> EnqueueVodaPayWebhookAsync(QueuedVodaPayWebhookJob job, CancellationToken cancellationToken)
        => Task.FromResult(true);
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

internal sealed class StubEmailDeliveryTrackingService : IEmailDeliveryTrackingService
{
    public Task<TrackedEmailDispatch> CreatePendingDispatchAsync(
        string providerKey,
        string templateName,
        string senderKey,
        string fromAddress,
        string recipientEmail,
        string subject,
        string bodyHtml,
        IReadOnlyCollection<EmailAttachment>? attachments,
        EmailTrackingContext? trackingContext,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new TrackedEmailDispatch
        {
            DispatchId = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid().ToString("N")
        });
    }

    public Task MarkAcceptedAsync(Guid dispatchId, string providerMessageId, string? providerBroadcastId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task MarkArchivedAsync(Guid dispatchId, string archivePath, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task MarkFailedAsync(Guid dispatchId, string errorMessage, DateTime? nextAttemptAt, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<EmailWebhookProcessResult> ProcessResendWebhookAsync(
        string requestPath,
        IReadOnlyDictionary<string, string> headers,
        string payload,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new EmailWebhookProcessResult
        {
            SignatureValid = true,
            ProcessingStatus = "accepted"
        });
    }
}

internal sealed class PassthroughEmailIntegrationSecretCipher : IEmailIntegrationSecretCipher
{
    public string? Protect(string? plainText) => plainText;
    public string? Unprotect(string? protectedValue) => protectedValue;
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
