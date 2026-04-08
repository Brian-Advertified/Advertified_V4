using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class WebsiteLeadSignalEvidenceProvider : ILeadSignalEvidenceProvider
{
    private readonly IWebsiteSignalProvider _websiteSignalProvider;

    public WebsiteLeadSignalEvidenceProvider(IWebsiteSignalProvider websiteSignalProvider)
    {
        _websiteSignalProvider = websiteSignalProvider;
    }

    public async Task<IReadOnlyList<LeadSignalEvidenceInput>> CollectAsync(Lead lead, CancellationToken cancellationToken)
    {
        var websiteSignals = await _websiteSignalProvider.CollectAsync(lead.Website, cancellationToken);
        var observedAt = websiteSignals.LastObservedAtUtc ?? DateTime.UtcNow;
        var evidence = new List<LeadSignalEvidenceInput>();

        if (websiteSignals.HasPromo)
        {
            evidence.Add(new LeadSignalEvidenceInput
            {
                Channel = "social",
                SignalType = "campaign_mention",
                Source = "website_scan",
                Confidence = "weakly_inferred",
                Weight = 15,
                ReliabilityMultiplier = 0.75m,
                IsPositive = true,
                ObservedAtUtc = observedAt,
                EvidenceUrl = websiteSignals.SourceUrl,
                Value = "Promotional messaging was detected on the company website."
            });
        }

        if (websiteSignals.HasMetaAds)
        {
            evidence.Add(new LeadSignalEvidenceInput
            {
                Channel = "social",
                SignalType = "meta_pixel_detected",
                Source = "website_scan",
                Confidence = "strongly_inferred",
                Weight = 20,
                ReliabilityMultiplier = 0.85m,
                IsPositive = true,
                ObservedAtUtc = observedAt,
                EvidenceUrl = websiteSignals.SourceUrl,
                Value = "Meta Pixel or Facebook tracking markers were detected on the website."
            });
        }

        if (websiteSignals.WebsiteUpdatedRecently)
        {
            evidence.Add(new LeadSignalEvidenceInput
            {
                Channel = "search",
                SignalType = "fresh_website",
                Source = "website_scan",
                Confidence = "weakly_inferred",
                Weight = 8,
                ReliabilityMultiplier = 0.7m,
                IsPositive = true,
                ObservedAtUtc = observedAt,
                EvidenceUrl = websiteSignals.SourceUrl,
                Value = "Website freshness suggests active digital campaign support."
            });
        }

        if (websiteSignals.HasLinkedInAdsTag)
        {
            evidence.Add(new LeadSignalEvidenceInput
            {
                Channel = "social",
                SignalType = "linkedin_insight_tag_detected",
                Source = "website_scan",
                Confidence = "strongly_inferred",
                Weight = 15,
                ReliabilityMultiplier = 0.8m,
                IsPositive = true,
                ObservedAtUtc = observedAt,
                EvidenceUrl = websiteSignals.SourceUrl,
                Value = "LinkedIn Insight Tag markers were detected on the website."
            });
        }

        if (websiteSignals.HasTikTokAdsTag)
        {
            evidence.Add(new LeadSignalEvidenceInput
            {
                Channel = "social",
                SignalType = "tiktok_pixel_detected",
                Source = "website_scan",
                Confidence = "strongly_inferred",
                Weight = 15,
                ReliabilityMultiplier = 0.8m,
                IsPositive = true,
                ObservedAtUtc = observedAt,
                EvidenceUrl = websiteSignals.SourceUrl,
                Value = "TikTok Pixel markers were detected on the website."
            });
        }

        return evidence;
    }
}
