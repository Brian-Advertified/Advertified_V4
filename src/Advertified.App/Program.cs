using Advertified.App.AIPlatform.Infrastructure;
using Advertified.App.Authentication;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Middleware;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Advertified.App.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Advertified")
    ?? throw new InvalidOperationException("Connection string 'Advertified' is not configured.");
const string FrontendCorsPolicy = "AdvertifiedFrontend";
var allowedFrontendOrigins = new[]
{
    "http://localhost:5173",
    "https://localhost:5173",
    "https://dev.advertified.com",
    "http://dev.advertified.com",
    "https://advertified.com",
    "https://www.advertified.com"
};

builder.Services
    .AddAdvertifiedPlatformConfiguration(builder.Configuration)
    .AddAdvertifiedApiInfrastructure(connectionString, allowedFrontendOrigins)
    .AddAdvertifiedApplicationServices()
    .AddAdvertifiedIntegrationServices()
    .AddAdvertifiedValidationServices()
    .AddAiAdvertisingPlatform();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var initializationScope = app.Services.CreateAsyncScope();
    var initializationDb = initializationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await initializationDb.Database.EnsureCreatedAsync();
}

await DatabaseSchemaInitializer.InitializeAsync(app.Services, app.Environment);
await LeadMasterDataValidator.ValidateAsync(app.Services);
await IndustryArchetypeScoringValidator.ValidateAsync(app.Services);
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
app.UseRateLimiter();
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

    var requestHostOrigin = $"{context.Request.Scheme}://{context.Request.Host}".TrimEnd('/');
    var allowed = allowedFrontendOrigins
        .Append(requestHostOrigin)
        .Any(origin => string.Equals(origin.TrimEnd('/'), requestOrigin, StringComparison.OrdinalIgnoreCase));

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

    await next();
});
app.UseAuthorization();
app.MapControllers();

app.Run();
