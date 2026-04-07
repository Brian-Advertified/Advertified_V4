namespace Advertified.App.Services.Abstractions;

public sealed class WebsiteSignalResult
{
    public bool HasPromo { get; init; }

    public bool HasMetaAds { get; init; }

    public bool WebsiteUpdatedRecently { get; init; }
}
