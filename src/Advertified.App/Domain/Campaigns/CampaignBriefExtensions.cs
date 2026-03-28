using System.Text.Json;
using Advertified.App.Data.Entities;
using CampaignBriefEntity = Advertified.App.Data.Entities.CampaignBrief;

namespace Advertified.App.Domain.Campaigns;

public static class CampaignBriefExtensions
{
    public static List<string> GetList(this CampaignBriefEntity brief, string propertyName)
    {
        var prop = typeof(CampaignBriefEntity).GetProperty(propertyName);
        var raw = prop?.GetValue(brief) as string;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        return JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
    }
}
