using System.Net;

namespace Advertified.App.Support;

public sealed class TransientHttpRetryHandler : DelegatingHandler
{
    private readonly ILogger<TransientHttpRetryHandler> _logger;

    public TransientHttpRetryHandler(ILogger<TransientHttpRetryHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var attemptRequest = attempt == 1
                ? null
                : await CloneHttpRequestMessageAsync(request, cancellationToken);
            var requestToSend = attemptRequest ?? request;

            try
            {
                var response = await base.SendAsync(requestToSend, cancellationToken);
                if (!ShouldRetry(response.StatusCode) || attempt == maxAttempts)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "Transient HTTP failure on attempt {Attempt}. Retrying request to {Uri}.", attempt, request.RequestUri);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                _logger.LogWarning("HTTP timeout on attempt {Attempt}. Retrying request to {Uri}.", attempt, request.RequestUri);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt * attempt), cancellationToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 408 || code == 429 || code >= 500;
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
