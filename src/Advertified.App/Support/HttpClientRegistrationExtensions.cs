using Advertified.App.Configuration;
using Microsoft.Extensions.Options;

namespace Advertified.App.Support;

internal static class HttpClientRegistrationExtensions
{
    internal static IHttpClientBuilder AddConfiguredOpenAiClient<TClient, TImplementation>(this IServiceCollection services)
        where TClient : class
        where TImplementation : class, TClient
    {
        return services
            .AddHttpClient<TClient, TImplementation>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<OpenAIOptions>>().Value;
                ConfigureClient(client, options.BaseUrl, options.TimeoutSeconds);
            })
            .AddHttpMessageHandler<TransientHttpRetryHandler>();
    }

    internal static IHttpClientBuilder AddConfiguredOpenAiClient(this IServiceCollection services, string clientName)
    {
        return services
            .AddHttpClient(clientName, (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<OpenAIOptions>>().Value;
                ConfigureClient(client, options.BaseUrl, options.TimeoutSeconds);
            })
            .AddHttpMessageHandler<TransientHttpRetryHandler>();
    }

    internal static IHttpClientBuilder AddConfiguredElevenLabsClient(this IServiceCollection services, string clientName)
    {
        return services
            .AddHttpClient(clientName, (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<ElevenLabsOptions>>().Value;
                ConfigureClient(client, options.BaseUrl, options.TimeoutSeconds);
            })
            .AddHttpMessageHandler<TransientHttpRetryHandler>();
    }

    private static void ConfigureClient(HttpClient client, string? baseUrl, int timeoutSeconds)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }

        if (timeoutSeconds > 0)
        {
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }
    }
}
