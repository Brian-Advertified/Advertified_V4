using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Enums;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Advertified.App.Validation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Advertified")
    ?? throw new InvalidOperationException("Connection string 'Advertified' is not configured.");
const string FrontendCorsPolicy = "AdvertifiedFrontend";

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection(FrontendOptions.SectionName));
builder.Services.Configure<MediaCatalogOptions>(builder.Configuration.GetSection(MediaCatalogOptions.SectionName));
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection(ResendOptions.SectionName));
builder.Services.Configure<UpstashQStashOptions>(builder.Configuration.GetSection(UpstashQStashOptions.SectionName));
builder.Services.Configure<UpstashRedisOptions>(builder.Configuration.GetSection(UpstashRedisOptions.SectionName));
builder.Services.Configure<VodaPayOptions>(builder.Configuration.GetSection(VodaPayOptions.SectionName));
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "https://localhost:5173")
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
builder.Services.AddScoped<ICampaignAccessService, CampaignAccessService>();
builder.Services.AddScoped<ICampaignBriefService, CampaignBriefService>();
builder.Services.AddScoped<ICampaignRecommendationService, CampaignRecommendationService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IMediaPlanningEngine, MediaPlanningEngine>();
builder.Services.AddScoped<IMediaCatalogSyncService, MediaCatalogSyncService>();
builder.Services.AddScoped<IPaymentAuditService, PaymentAuditService>();
builder.Services.AddScoped<IPackageCatalogService, PackageCatalogService>();
builder.Services.AddScoped<IPlanningInventoryRepository>(_ => new PlanningInventoryRepository(connectionString));
builder.Services.AddScoped<IPackagePreviewService>(_ => new PackagePreviewService(
    _.GetRequiredService<AppDbContext>(),
    connectionString));
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

var app = builder.Build();

await DatabaseSchemaInitializer.InitializeAsync(app.Services, app.Environment);
await EmailTemplateInitializer.InitializeAsync(app.Services);
await PackageCatalogInitializer.InitializeAsync(app.Services);
await using (var scope = app.Services.CreateAsyncScope())
{
    var mediaCatalogSyncService = scope.ServiceProvider.GetRequiredService<IMediaCatalogSyncService>();
    await mediaCatalogSyncService.SyncAsync(CancellationToken.None);
}

app.UseCors(FrontendCorsPolicy);
app.MapControllers();

app.Run();
