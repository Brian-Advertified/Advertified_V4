using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class TrendAnalysisService : ITrendAnalysisService
{
    public LeadTrendAnalysisResult Analyze(Signal? previousSignal, Signal currentSignal)
    {
        if (previousSignal is null)
        {
            return new LeadTrendAnalysisResult
            {
                Summary = "Initial baseline captured for this lead.",
                CampaignStartedRecently = currentSignal.HasPromo || currentSignal.HasMetaAds,
                ActivityIncreased = currentSignal.HasPromo || currentSignal.HasMetaAds || currentSignal.WebsiteUpdatedRecently,
            };
        }

        var changes = new List<string>();
        var campaignStartedRecently = false;

        if (!previousSignal.HasPromo && currentSignal.HasPromo)
        {
            changes.Add("Promotion activity just appeared.");
            campaignStartedRecently = true;
        }
        else if (previousSignal.HasPromo && !currentSignal.HasPromo)
        {
            changes.Add("Promotion activity appears to have stopped.");
        }

        if (!previousSignal.HasMetaAds && currentSignal.HasMetaAds)
        {
            changes.Add("Meta ad activity was newly detected.");
            campaignStartedRecently = true;
        }
        else if (previousSignal.HasMetaAds && !currentSignal.HasMetaAds)
        {
            changes.Add("Previously detected Meta ad activity is no longer visible.");
        }

        if (!previousSignal.WebsiteUpdatedRecently && currentSignal.WebsiteUpdatedRecently)
        {
            changes.Add("Website freshness improved recently.");
        }
        else if (previousSignal.WebsiteUpdatedRecently && !currentSignal.WebsiteUpdatedRecently)
        {
            changes.Add("Website freshness signal cooled off.");
        }

        if (changes.Count == 0)
        {
            changes.Add("No material signal change detected since the previous analysis.");
        }

        return new LeadTrendAnalysisResult
        {
            Summary = string.Join(" ", changes),
            CampaignStartedRecently = campaignStartedRecently,
            ActivityIncreased =
                (!previousSignal.HasPromo && currentSignal.HasPromo) ||
                (!previousSignal.HasMetaAds && currentSignal.HasMetaAds) ||
                (!previousSignal.WebsiteUpdatedRecently && currentSignal.WebsiteUpdatedRecently),
        };
    }
}
