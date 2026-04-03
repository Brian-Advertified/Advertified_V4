using Advertified.App.Configuration;
using Advertified.App.AIPlatform.Infrastructure;
using Advertified.App.Data;
using Advertified.App.Data.Enums;
using Advertified.App.Middleware;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Services.BroadcastMatching;
using Advertified.App.Support;
using Advertified.App.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Advertified")
    ?? throw new InvalidOperationException("Connection string 'Advertified' is not configured.");
const string FrontendCorsPolicy = "AdvertifiedFrontend";

builder.Services.AddControllers();
builder.Services.AddDataProtection();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection(FrontendOptions.SectionName));
builder.Services.Configure<BroadcastInventoryOptions>(builder.Configuration.GetSection(BroadcastInventoryOptions.SectionName));
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection(ResendOptions.SectionName));
builder.Services.Configure<UpstashQStashOptions>(builder.Configuration.GetSection(UpstashQStashOptions.SectionName));
builder.Services.Configure<UpstashRedisOptions>(builder.Configuration.GetSection(UpstashRedisOptions.SectionName));
builder.Services.Configure<VodaPayOptions>(builder.Configuration.GetSection(VodaPayOptions.SectionName));
builder.Services.Configure<PlanningPolicyOptions>(builder.Configuration.GetSection(PlanningPolicyOptions.SectionName));
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection(OpenAIOptions.SectionName));
builder.Services.Configure<ElevenLabsOptions>(builder.Configuration.GetSection(ElevenLabsOptions.SectionName));
builder.Services.Configure<AiPlatformOptions>(builder.Configuration.GetSection(AiPlatformOptions.SectionName));
builder.Services.Configure<AdPlatformOptions>(builder.Configuration.GetSection(AdPlatformOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<InventoryReadinessOptions>(builder.Configuration.GetSection(InventoryReadinessOptions.SectionName));
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "https://localhost:5173",
                "https://dev.advertified.com",
                "http://dev.advertified.com",
                "https://advertified.com",
                "https://www.advertified.com")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.MapEnum<AccountStatus>("account_status");
            npgsqlOptions.MapEnum<IdentityType>("identity_type");
            npgsqlOptions.MapEnum<UserRole>("user_role");
            npgsqlOptions.MapEnum<VerificationStatus>("verification_status");
        }));
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<ISessionTokenService, SessionTokenService>();
builder.Services.AddScoped<IProposalAccessTokenService, ProposalAccessTokenService>();
builder.Services.AddScoped<IPasswordHashingService, PasswordHashingService>();
builder.Services.AddScoped<IPricingSettingsProvider, PricingSettingsProvider>();
builder.Services.AddScoped<IAdminDashboardService>(_ => new AdminDashboardService(
    _.GetRequiredService<AppDbContext>(),
    _.GetRequiredService<IBroadcastInventoryCatalog>(),
    _.GetRequiredService<PlanningPolicySnapshotProvider>(),
    connectionString));
builder.Services.AddScoped<IAdminMutationService>(_ => new AdminMutationService(
    connectionString,
    _.GetRequiredService<IWebHostEnvironment>()));
builder.Services.AddScoped<ICampaignAccessService, CampaignAccessService>();
builder.Services.AddScoped<IAgentAreaRoutingService, AgentAreaRoutingService>();
builder.Services.AddScoped<ICampaignBriefService, CampaignBriefService>();
builder.Services.AddScoped<ICampaignRecommendationService, CampaignRecommendationService>();
builder.Services.AddScoped<IRecommendationDocumentService, RecommendationDocumentService>();
builder.Services.AddScoped<IRecommendationApprovalWorkflowService, RecommendationApprovalWorkflowService>();
builder.Services.AddScoped<ICreativeGenerationOrchestrator, CreativeGenerationOrchestrator>();
builder.Services.AddScoped<ICreativeStudioIntelligenceService, CreativeStudioIntelligenceService>();
builder.Services.AddHttpClient<IPublicAssetStorage, PublicAssetStorageService>();
builder.Services.AddHttpClient<IPrivateDocumentStorage, PrivateDocumentStorageService>();
builder.Services.AddSingleton<IBroadcastInventoryCatalog>(_ => new BroadcastInventoryCatalog(connectionString));
builder.Services.AddSingleton<IBroadcastCostNormalizer, BroadcastCostNormalizer>();
builder.Services.AddSingleton<IBroadcastInventoryImportService>(_ =>
    new BroadcastInventoryImportService(
        connectionString,
        _.GetRequiredService<Microsoft.Extensions.Options.IOptions<BroadcastInventoryOptions>>(),
        _.GetRequiredService<IWebHostEnvironment>()));
builder.Services.AddHttpClient<ICampaignBriefInterpretationService, CampaignBriefInterpretationService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAIOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    }

    if (options.TimeoutSeconds > 0)
    {
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }
});
builder.Services.AddHttpClient<ICampaignReasoningService, OpenAICampaignReasoningService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAIOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    }

    if (options.TimeoutSeconds > 0)
    {
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }
});
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddSingleton(BroadcastMatcherPolicy.Default);
builder.Services.AddScoped<IBroadcastMatchRequestNormalizer, BroadcastMatchRequestNormalizer>();
builder.Services.AddScoped<IBroadcastMatchRequestValidator, BroadcastMatchRequestValidator>();
builder.Services.AddScoped<IBroadcastHardFilterEngine, BroadcastHardFilterEngine>();
builder.Services.AddScoped<IBroadcastScoreCalculator, BroadcastScoreCalculator>();
builder.Services.AddScoped<IBroadcastRecommendationRanker, BroadcastRecommendationRanker>();
builder.Services.AddScoped<IBroadcastMatchingEngine, BroadcastMatchingEngine>();
builder.Services.AddScoped<IPlanningCandidateLoader, PlanningCandidateLoader>();
builder.Services.AddScoped<IPlanningPolicyService, PlanningPolicyService>();
builder.Services.AddScoped(_ => new PlanningPolicySnapshotProvider(
    connectionString,
    _.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlanningPolicyOptions>>().Value));
builder.Services.AddScoped<IPlanningEligibilityService, PlanningEligibilityService>();
builder.Services.AddScoped<IPlanningScoreService, PlanningScoreService>();
builder.Services.AddScoped<IRecommendationPlanBuilder, RecommendationPlanBuilder>();
builder.Services.AddScoped<IRecommendationExplainabilityService, RecommendationExplainabilityService>();
builder.Services.AddScoped<IOohPlanningInventorySource>(_ => new OohPlanningInventorySource(
    connectionString,
    _.GetRequiredService<IPricingSettingsProvider>()));
builder.Services.AddScoped<IBroadcastPlanningInventorySource, BroadcastPlanningInventorySource>();
builder.Services.AddScoped<IPlanningInventoryCandidateMapper, PlanningInventoryCandidateMapper>();
builder.Services.AddScoped<IMediaPlanningEngine>(_ => new MediaPlanningEngine(
    _.GetRequiredService<IPlanningCandidateLoader>(),
    _.GetRequiredService<IPlanningEligibilityService>(),
    _.GetRequiredService<IRecommendationPlanBuilder>(),
    _.GetRequiredService<IRecommendationExplainabilityService>()));
builder.Services.AddScoped<IPaymentAuditService, PaymentAuditService>();
builder.Services.AddScoped<IChangeAuditService, ChangeAuditService>();
builder.Services.AddScoped<IPackageAreaService, PackageAreaService>();
builder.Services.AddScoped<IPackageCatalogService, PackageCatalogService>();
builder.Services.AddScoped<IPackagePreviewAreaProfileResolver, PackagePreviewAreaProfileResolver>();
builder.Services.AddScoped<IPackagePreviewReachEstimator, PackagePreviewReachEstimator>();
builder.Services.AddScoped<IPackagePreviewOutdoorSelector, PackagePreviewOutdoorSelector>();
builder.Services.AddScoped<IPackagePreviewBroadcastSelector, PackagePreviewBroadcastSelector>();
builder.Services.AddScoped<IPackagePreviewFormatter, PackagePreviewFormatter>();
builder.Services.AddScoped<IPlanningInventoryRepository, PlanningInventoryRepository>();
builder.Services.AddScoped<IPackagePreviewService>(_ => new PackagePreviewService(
    _.GetRequiredService<AppDbContext>(),
    connectionString,
    _.GetRequiredService<IBroadcastInventoryCatalog>(),
    _.GetRequiredService<IPackagePreviewAreaProfileResolver>(),
    _.GetRequiredService<IPackagePreviewReachEstimator>(),
    _.GetRequiredService<IPackagePreviewOutdoorSelector>(),
    _.GetRequiredService<IPackagePreviewBroadcastSelector>(),
    _.GetRequiredService<IPackagePreviewFormatter>()));
builder.Services.AddScoped<IPackagePurchaseService, PackagePurchaseService>();
builder.Services.AddHttpClient<IWebhookQueueService, UpstashQStashWebhookQueueService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpstashQStashOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    }
});
builder.Services.AddHttpClient<IPaymentStateCache, UpstashPaymentStateCache>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpstashRedisOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(options.RestUrl))
    {
        client.BaseAddress = new Uri(options.RestUrl.TrimEnd('/') + "/");
    }
});
builder.Services.AddHttpClient<IVodaPayCheckoutService, VodaPayCheckoutService>((serviceProvider, client) =>
{
    var vodaPayOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<VodaPayOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(vodaPayOptions.BaseUrl))
    {
        client.BaseAddress = new Uri(vodaPayOptions.BaseUrl.TrimEnd('/') + "/");
    }
});
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddHttpClient<ITemplatedEmailService, ResendEmailService>((serviceProvider, client) =>
{
    var resendOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ResendOptions>>().Value;
    client.BaseAddress = new Uri(resendOptions.BaseUrl.TrimEnd('/') + "/");
});
builder.Services.AddScoped<CampaignPlanningRequestValidator>();
builder.Services.AddScoped<RegisterRequestValidator>();
builder.Services.AddScoped<SaveCampaignBriefRequestValidator>();
builder.Services.AddAiAdvertisingPlatform();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var initializationScope = app.Services.CreateAsyncScope();
    var initializationDb = initializationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await initializationDb.Database.EnsureCreatedAsync();
}

await DatabaseSchemaInitializer.InitializeAsync(app.Services, app.Environment);
await EmailTemplateInitializer.InitializeAsync(app.Services);
await PackageCatalogInitializer.InitializeAsync(app.Services);
await using (var scope = app.Services.CreateAsyncScope())
{
    var broadcastInventoryImportService = scope.ServiceProvider.GetRequiredService<IBroadcastInventoryImportService>();
    await broadcastInventoryImportService.SyncAsync(CancellationToken.None);
}
await InventoryReadinessValidator.ValidateAsync(app.Services, connectionString, app.Logger, CancellationToken.None);

app.UseMiddleware<ProblemDetailsExceptionHandlingMiddleware>();

app.UseCors(FrontendCorsPolicy);
app.MapControllers();

app.Run();
