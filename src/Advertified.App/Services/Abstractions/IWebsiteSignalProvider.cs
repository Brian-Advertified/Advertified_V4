namespace Advertified.App.Services.Abstractions;

public interface IWebsiteSignalProvider
{
    Task<WebsiteSignalResult> CollectAsync(string? websiteUrl, CancellationToken cancellationToken);
}
