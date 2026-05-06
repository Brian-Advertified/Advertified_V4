using Advertified.App.AIPlatform.Infrastructure;
using Advertified.App.Authentication;
using Advertified.App.Data;
using Advertified.App.Data.Enums;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Services.BroadcastMatching;
using Advertified.App.Support;
using Advertified.App.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System.Threading.RateLimiting;

namespace Advertified.App.Configuration;

public static class AdvertifiedServiceCollectionExtensions
{
    public static IServiceCollection AddAdvertifiedPlatformConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HostOptions>(options =>
        {
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
        });
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        services.Configure<BroadcastInventoryOptions>(configuration.GetSection(BroadcastInventoryOptions.SectionName));
        services.Configure<ResendOptions>(configuration.GetSection(ResendOptions.SectionName));
        services.Configure<UpstashQStashOptions>(configuration.GetSection(UpstashQStashOptions.SectionName));
        services.Configure<UpstashRedisOptions>(configuration.GetSection(UpstashRedisOptions.SectionName));
        services.Configure<VodaPayOptions>(configuration.GetSection(VodaPayOptions.SectionName));
        services.Configure<PlanningPolicyOptions>(configuration.GetSection(PlanningPolicyOptions.SectionName));
        services.Configure<LeadScoringOptions>(configuration.GetSection(LeadScoringOptions.SectionName));
        services.Configure<LeadIndustryPolicyOptions>(configuration.GetSection(LeadIndustryPolicyOptions.SectionName));
        services.Configure<LeadIntelligenceAutomationOptions>(configuration.GetSection(LeadIntelligenceAutomationOptions.SectionName));
        services.Configure<LeadSourceDropFolderOptions>(configuration.GetSection(LeadSourceDropFolderOptions.SectionName));
        services.Configure<GoogleSheetsLeadOpsOptions>(configuration.GetSection(GoogleSheetsLeadOpsOptions.SectionName));
        services.Configure<OpenAIOptions>(configuration.GetSection(OpenAIOptions.SectionName));
        services.Configure<ElevenLabsOptions>(configuration.GetSection(ElevenLabsOptions.SectionName));
        services.Configure<AiPlatformOptions>(configuration.GetSection(AiPlatformOptions.SectionName));
        services.Configure<AdPlatformOptions>(configuration.GetSection(AdPlatformOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<InventoryReadinessOptions>(configuration.GetSection(InventoryReadinessOptions.SectionName));
        return services;
    }

    public static IServiceCollection AddAdvertifiedApiInfrastructure(this IServiceCollection services, string connectionString, string[] allowedFrontendOrigins)
    {
        const string frontendCorsPolicy = "AdvertifiedFrontend";

        services.AddControllers();
        services.AddMemoryCache();
        services.AddDataProtection();
        services.AddHttpContextAccessor();
        services.AddTransient<TransientHttpRetryHandler>();
        services.AddCors(options =>
        {
            options.AddPolicy(frontendCorsPolicy, policy =>
            {
                policy
                    .WithOrigins(allowedFrontendOrigins)
                    .AllowCredentials()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("auth", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 12,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy("public_proposal", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy("public_general", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
        });
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MapEnum<AccountStatus>("account_status");
                    npgsqlOptions.MapEnum<IdentityType>("identity_type");
                    npgsqlOptions.MapEnum<UserRole>("user_role");
                    npgsqlOptions.MapEnum<VerificationStatus>("verification_status");
                }));
        services.AddSingleton<Npgsql.NpgsqlDataSource>(_ =>
            new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build());

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "AdvertifiedSession";
            options.DefaultChallengeScheme = "AdvertifiedSession";
        })
        .AddScheme<AuthenticationSchemeOptions, SessionTokenAuthenticationHandler>("AdvertifiedSession", options => { });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    public static IServiceCollection AddAdvertifiedApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<ISessionTokenService, SessionTokenService>();
        services.AddScoped<IProposalAccessTokenService, ProposalAccessTokenService>();
        services.AddScoped<IPasswordHashingService, PasswordHashingService>();
        services.AddScoped<FormOptionsService>();
        services.AddScoped<IPricingSettingsProvider, PricingSettingsProvider>();
        services.AddScoped<IBroadcastMasterDataService, BroadcastMasterDataService>();
        services.AddScoped<IBroadcastLanguagePriorityService, BroadcastLanguagePriorityService>();
        services.AddScoped<IBroadcastInventoryIntelligenceService, BroadcastInventoryIntelligenceService>();
        services.AddScoped<IAdminDashboardService>(_ => new AdminDashboardService(
            _.GetRequiredService<AppDbContext>(),
            _.GetRequiredService<IBroadcastInventoryCatalog>(),
            _.GetRequiredService<PlanningPolicySnapshotProvider>(),
            _.GetRequiredService<PlanningBudgetAllocationSnapshotProvider>(),
            _.GetRequiredService<Npgsql.NpgsqlDataSource>(),
            _.GetRequiredService<IBroadcastMasterDataService>(),
            _.GetRequiredService<AdminIntegrationStatusService>()));
        services.AddScoped<IAdminMutationService>(_ => new AdminMutationService(
            _.GetRequiredService<Npgsql.NpgsqlDataSource>(),
            _.GetRequiredService<IWebHostEnvironment>(),
            _.GetRequiredService<IBroadcastInventoryCatalog>(),
            _.GetRequiredService<IBroadcastMasterDataService>()));
        services.AddScoped<IAdminLeadIndustryPolicyService, AdminLeadIndustryPolicyService>();
        services.AddScoped<IAdminIndustryStrategyProfileService, AdminIndustryStrategyProfileService>();
        services.AddScoped<IAdminLeadIntelligenceSettingsService, AdminLeadIntelligenceSettingsService>();
        services.AddScoped<ICampaignAccessService, CampaignAccessService>();
        services.AddScoped<IAgentCampaignOwnershipService, AgentCampaignOwnershipService>();
        services.AddScoped<IAgentAreaRoutingService, AgentAreaRoutingService>();
        services.AddScoped<ICampaignBriefService, CampaignBriefService>();
        services.AddScoped<ICampaignRecommendationService, CampaignRecommendationService>();
        services.AddScoped<IRecommendationProposalIntelligenceService, RecommendationProposalIntelligenceService>();
        services.AddScoped<IRecommendationDocumentService, RecommendationDocumentService>();
        services.AddScoped<IRecommendationApprovalWorkflowService, RecommendationApprovalWorkflowService>();
        services.AddScoped<ICampaignStatusTransitionService, CampaignStatusTransitionService>();
        services.AddScoped<ICampaignExecutionTaskService, CampaignExecutionTaskService>();
        services.AddScoped<IProspectDispositionService, ProspectDispositionService>();
        services.AddScoped<IAgentCampaignWorkflowOrchestrationService, AgentCampaignWorkflowOrchestrationService>();
        services.AddScoped<IAgentCampaignBookingOrchestrationService, AgentCampaignBookingOrchestrationService>();
        services.AddScoped<ICreativeGenerationOrchestrator, CreativeGenerationOrchestrator>();
        services.AddScoped<ICreativeStudioIntelligenceService, CreativeStudioIntelligenceService>();
        services.AddSingleton<IBroadcastInventoryCatalog>(_ => new BroadcastInventoryCatalog(_.GetRequiredService<Npgsql.NpgsqlDataSource>()));
        services.AddSingleton<IBroadcastCostNormalizer, BroadcastCostNormalizer>();
        services.AddSingleton(BroadcastMatcherPolicy.Default);
        services.AddScoped<IBroadcastMatchRequestNormalizer, BroadcastMatchRequestNormalizer>();
        services.AddScoped<IBroadcastMatchRequestValidator, BroadcastMatchRequestValidator>();
        services.AddScoped<IBroadcastHardFilterEngine, BroadcastHardFilterEngine>();
        services.AddScoped<IBroadcastScoreCalculator, BroadcastScoreCalculator>();
        services.AddScoped<IBroadcastRecommendationRanker, BroadcastRecommendationRanker>();
        services.AddScoped<IBroadcastMatchingEngine, BroadcastMatchingEngine>();
        services.AddScoped<IPlanningCandidateLoader, PlanningCandidateLoader>();
        services.AddScoped<IPlanningPolicyService, PlanningPolicyService>();
        services.AddScoped<PlanningPolicySnapshotProvider>(_ => new PlanningPolicySnapshotProvider(
            _.GetRequiredService<Npgsql.NpgsqlDataSource>(),
            _.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlanningPolicyOptions>>().Value));
        services.AddScoped<PlanningBudgetAllocationSnapshotProvider>(_ =>
            new PlanningBudgetAllocationSnapshotProvider(_.GetRequiredService<Npgsql.NpgsqlDataSource>()));
        services.AddScoped<IPlanningBudgetAllocationService, PlanningBudgetAllocationService>();
        services.AddScoped<PlanningBriefIntentSettingsSnapshotProvider>(_ =>
            new PlanningBriefIntentSettingsSnapshotProvider(_.GetRequiredService<Npgsql.NpgsqlDataSource>()));
        services.AddScoped<IPlanningBriefIntentService, PlanningBriefIntentService>();
        services.AddScoped<ICommercialFlightPricingResolver, CommercialFlightPricingResolver>();
        services.AddScoped<LeadIndustryPolicySnapshotProvider>(_ => new LeadIndustryPolicySnapshotProvider(
            _.GetRequiredService<Npgsql.NpgsqlDataSource>(),
            _.GetRequiredService<Microsoft.Extensions.Options.IOptions<LeadIndustryPolicyOptions>>().Value));
        services.AddScoped<LeadScoringSettingsSnapshotProvider>(_ => new LeadScoringSettingsSnapshotProvider(
            _.GetRequiredService<Npgsql.NpgsqlDataSource>(),
            _.GetRequiredService<Microsoft.Extensions.Options.IOptions<LeadScoringOptions>>().Value));
        services.AddScoped<LeadIntelligenceAutomationSnapshotProvider>(_ => new LeadIntelligenceAutomationSnapshotProvider(
            _.GetRequiredService<Npgsql.NpgsqlDataSource>(),
            _.GetRequiredService<Microsoft.Extensions.Options.IOptions<LeadIntelligenceAutomationOptions>>().Value));
        services.AddScoped<IPlanningEligibilityService, PlanningEligibilityService>();
        services.AddScoped<IPlanningScoreService, PlanningScoreService>();
        services.AddScoped<IRecommendationPlanBuilder>(_ => new RecommendationPlanBuilder(
            _.GetRequiredService<IPlanningPolicyService>(),
            _.GetRequiredService<IBroadcastMasterDataService>()));
        services.AddScoped<IRecommendationExplainabilityService, RecommendationExplainabilityService>();
        services.AddScoped<IOohPlanningInventorySource>(_ => new OohPlanningInventorySource(
            _.GetRequiredService<Npgsql.NpgsqlDataSource>(),
            _.GetRequiredService<ICommercialFlightPricingResolver>(),
            _.GetRequiredService<IPricingSettingsProvider>()));
        services.AddScoped<IBroadcastPlanningInventorySource>(_ => new BroadcastPlanningInventorySource(
            _.GetRequiredService<IBroadcastInventoryCatalog>(),
            _.GetRequiredService<IBroadcastCostNormalizer>(),
            _.GetRequiredService<IPricingSettingsProvider>(),
            _.GetRequiredService<IBroadcastInventoryIntelligenceService>(),
            _.GetRequiredService<ICommercialFlightPricingResolver>()));
        services.AddScoped<ISocialPlanningInventorySource, SocialPlanningInventorySource>();
        services.AddScoped<IPlanningInventoryCandidateMapper, PlanningInventoryCandidateMapper>();
        services.AddScoped<IMediaPlanningEngine>(_ => new MediaPlanningEngine(
            _.GetRequiredService<IPlanningCandidateLoader>(),
            _.GetRequiredService<IPlanningEligibilityService>(),
            _.GetRequiredService<IRecommendationPlanBuilder>(),
            _.GetRequiredService<IRecommendationExplainabilityService>(),
            _.GetRequiredService<IPlanningPolicyService>(),
            _.GetRequiredService<IBroadcastLanguagePriorityService>()));
        services.AddScoped<IPaymentAuditService, PaymentAuditService>();
        services.AddScoped<IChangeAuditService, ChangeAuditService>();
        services.AddScoped<IPackageAreaService, PackageAreaService>();
        services.AddScoped<IPackageCatalogService, PackageCatalogService>();
        services.AddScoped<IPackagePreviewAreaProfileResolver, PackagePreviewAreaProfileResolver>();
        services.AddScoped<IPackagePreviewReachEstimator, PackagePreviewReachEstimator>();
        services.AddScoped<IPackagePreviewOutdoorSelector, PackagePreviewOutdoorSelector>();
        services.AddScoped<IPackagePreviewBroadcastSelector, PackagePreviewBroadcastSelector>();
        services.AddScoped<IPackagePreviewFormatter, PackagePreviewFormatter>();
        services.AddScoped<IPlanningInventoryRepository, PlanningInventoryRepository>();
        services.AddScoped<IPackagePreviewService>(_ => new PackagePreviewService(
            _.GetRequiredService<AppDbContext>(),
            _.GetRequiredService<Npgsql.NpgsqlDataSource>(),
            _.GetRequiredService<IBroadcastInventoryCatalog>(),
            _.GetRequiredService<IPackagePreviewAreaProfileResolver>(),
            _.GetRequiredService<IPackagePreviewReachEstimator>(),
            _.GetRequiredService<IPackagePreviewOutdoorSelector>(),
            _.GetRequiredService<IPackagePreviewBroadcastSelector>(),
            _.GetRequiredService<IPackagePreviewFormatter>()));
        services.AddScoped<IPackagePurchaseService, PackagePurchaseService>();
        services.AddScoped<IProspectLeadLinkingService, ProspectLeadLinkingService>();
        services.AddScoped<IProspectLeadRegistrationService, ProspectLeadRegistrationService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        return services;
    }

    public static IServiceCollection AddAdvertifiedIntegrationServices(this IServiceCollection services)
    {
        services.AddHttpClient<IPublicAssetStorage, PublicAssetStorageService>();
        services.AddHttpClient<IPrivateDocumentStorage, PrivateDocumentStorageService>();
        services.AddSingleton<IBroadcastInventoryImportService>(_ =>
            new BroadcastInventoryImportService(
                _.GetRequiredService<Npgsql.NpgsqlDataSource>(),
                _.GetRequiredService<Microsoft.Extensions.Options.IOptions<BroadcastInventoryOptions>>(),
                _.GetRequiredService<IWebHostEnvironment>(),
                _.GetRequiredService<IBroadcastInventoryCatalog>(),
                _.GetRequiredService<ILogger<BroadcastInventoryImportService>>()));
        services.AddConfiguredOpenAiClient<ICampaignBriefInterpretationService, CampaignBriefInterpretationService>();
        services.AddConfiguredOpenAiClient<ICampaignReasoningService, OpenAICampaignReasoningService>();
        services.AddConfiguredOpenAiClient(nameof(OpenAiProviderStrategy));
        services.AddConfiguredElevenLabsClient(nameof(ElevenLabsProviderStrategy));
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IAdPlatformAccessTokenService, AdPlatformAccessTokenService>();
        services.AddScoped<IAdPlatformTokenCipher, AdPlatformTokenCipher>();
        services.AddScoped<IEmailIntegrationSecretCipher, EmailIntegrationSecretCipher>();
        services.AddScoped<IEmailDeliveryTrackingService, EmailDeliveryTrackingService>();
        services.AddScoped<IAdPlatformConnectionService, AdPlatformConnectionService>();
        services.AddScoped<ILeadIntelligenceOrchestrator, LeadIntelligenceOrchestrator>();
        services.AddScoped<ILeadActionRecommendationService, LeadActionRecommendationService>();
        services.AddScoped<ILeadSourceAutomationStatusService, LeadSourceAutomationStatusService>();
        services.AddScoped<ILeadChannelDetectionService, LeadChannelDetectionService>();
        services.AddScoped<ILeadMasterDataService, LeadMasterDataService>();
        services.AddScoped<IIndustryArchetypeScoringService, IndustryArchetypeScoringService>();
        services.AddScoped<IIndustryStrategyCatalogService, IndustryStrategyCatalogService>();
        services.AddScoped<ILeadIndustryContextResolver, LeadIndustryContextResolver>();
        services.AddScoped<IGeocodingService, GeocodingService>();
        services.AddScoped<ILocationCatalogService, LocationCatalogService>();
        services.AddScoped<ICampaignBusinessLocationResolver, CampaignBusinessLocationResolver>();
        services.AddScoped<ICampaignPlanningTargetResolver, CampaignPlanningTargetResolver>();
        services.AddScoped<IPlanningRequestFactory, PlanningRequestFactory>();
        services.AddScoped<ILeadIndustryPolicyService, LeadIndustryPolicyService>();
        services.AddScoped<ILeadOpportunityProfileService, LeadOpportunityProfileService>();
        services.AddScoped<ILeadEnrichmentSnapshotService, LeadEnrichmentSnapshotService>();
        services.AddScoped<ILeadBusinessProfileService, LeadBusinessProfileService>();
        services.AddScoped<ILeadStrategyEngine, LeadStrategyEngine>();
        services.AddScoped<ILeadProposalConfidenceGateService, LeadProposalConfidenceGateService>();
        services.AddScoped<ILeadScoreService, LeadScoreService>();
        services.AddScoped<ILeadSourceDropFolderProcessor, LeadSourceDropFolderProcessor>();
        services.AddScoped<ILeadSourceIngestionService, LeadSourceIngestionService>();
        services.AddScoped<ILeadSourceImportService, LeadSourceImportService>();
        services.AddScoped<ILeadOpsCoverageService, LeadOpsCoverageService>();
        services.AddScoped<ILeadOpsStateService, LeadOpsStateService>();
        services.AddScoped<ILeadOpsInboxService, LeadOpsInboxService>();
        services.AddScoped<IPublicLocationSearchService, PublicLocationSearchService>();
        services.AddScoped<ISignalCollectorService, SignalCollectorService>();
        services.AddScoped<ILeadSignalEvidenceProvider, WebsiteLeadSignalEvidenceProvider>();
        services.AddHttpClient<MetaAdLibraryLeadSignalEvidenceProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        }).AddHttpMessageHandler<TransientHttpRetryHandler>();
        services.AddHttpClient<GoogleAdsEvidenceProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        }).AddHttpMessageHandler<TransientHttpRetryHandler>();
        services.AddScoped<ILeadSignalEvidenceProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<MetaAdLibraryLeadSignalEvidenceProvider>());
        services.AddScoped<ILeadSignalEvidenceProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<GoogleAdsEvidenceProvider>());
        services.AddScoped<ILeadPaidMediaEvidenceSyncService, LeadPaidMediaEvidenceSyncService>();
        services.AddScoped<ITrendAnalysisService, TrendAnalysisService>();
        services.AddConfiguredOpenAiClient<IInsightService, InsightService>();
        services.AddHttpClient<IWebsiteSignalProvider, WebsiteSignalProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient<IGoogleSheetsLeadIntegrationService, GoogleSheetsLeadIntegrationService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddHttpMessageHandler<TransientHttpRetryHandler>();
        services.AddHostedService<LeadIntelligenceRefreshWorker>();
        services.AddHostedService<LeadPaidMediaEvidenceSyncWorker>();
        services.AddHostedService<LeadSourceDropFolderWorker>();
        services.AddHostedService<GoogleSheetsLeadIntegrationWorker>();
        services.AddHttpClient<IWebhookQueueService, UpstashQStashWebhookQueueService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpstashQStashOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            }
        });
        services.AddHttpClient<IPaymentStateCache, UpstashPaymentStateCache>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpstashRedisOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.RestUrl))
            {
                client.BaseAddress = new Uri(options.RestUrl.TrimEnd('/') + "/");
            }
        });
        services.AddHttpClient<IVodaPayCheckoutService, VodaPayCheckoutService>((serviceProvider, client) =>
        {
            var vodaPayOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<VodaPayOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(vodaPayOptions.BaseUrl))
            {
                client.BaseAddress = new Uri(vodaPayOptions.BaseUrl.TrimEnd('/') + "/");
            }
        }).AddHttpMessageHandler<TransientHttpRetryHandler>();
        services.AddScoped<ITemplatedEmailService, ResendEmailService>();
        services.AddScoped<AdminIntegrationStatusService>();
        services.AddHttpClient<ResendEmailTransport>((serviceProvider, client) =>
        {
            var resendOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ResendOptions>>().Value;
            client.BaseAddress = new Uri(resendOptions.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<ResendEmailOutboxDispatcher>();
        services.AddHostedService<ResendEmailOutboxWorker>();
        return services;
    }

    public static IServiceCollection AddAdvertifiedValidationServices(this IServiceCollection services)
    {
        services.AddScoped<CampaignPlanningRequestValidator>();
        services.AddScoped<RegisterRequestValidator>();
        services.AddScoped<SaveCampaignBriefRequestValidator>();
        return services;
    }
}
