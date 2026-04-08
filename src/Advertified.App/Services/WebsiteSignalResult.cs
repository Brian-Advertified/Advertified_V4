namespace Advertified.App.Services.Abstractions;

public sealed class WebsiteSignalResult
{
    public bool HasPromo { get; init; }

    public bool HasMetaAds { get; init; }

    public bool WebsiteUpdatedRecently { get; init; }

    public bool HasLinkedInAdsTag { get; init; }

    public bool HasTikTokAdsTag { get; init; }

    public string? SourceUrl { get; init; }

    public DateTime? LastObservedAtUtc { get; init; }
}
