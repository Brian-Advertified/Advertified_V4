using Advertified.AIPlatform.Application.Abstractions;
using Advertified.AIPlatform.Application.Services;
using Advertified.AIPlatform.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Advertified.AIPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAiPlatformCore(this IServiceCollection services)
    {
        services.AddScoped<IAiProviderStrategy, OpenAiProviderStrategy>();
        services.AddScoped<IAiProviderStrategy, ElevenLabsProviderStrategy>();
        services.AddScoped<IAiProviderStrategy, RunwayProviderStrategy>();
        services.AddScoped<IAiProviderStrategy, ImageProviderStrategy>();
        services.AddScoped<IAiProviderStrategyFactory, AiProviderStrategyFactory>();
        services.AddScoped<IMultiAiOrchestrator, MultiAiOrchestrator>();
        services.AddScoped<ICreativeGenerationService, CreativeGenerationService>();
        return services;
    }
}
