namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignBusinessLocation
{
    public string Label { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Source { get; set; } = "none";
    public string Precision { get; set; } = "unknown";
    public bool IsResolved { get; set; }

    public CampaignBusinessLocation DeepClone()
    {
        return new CampaignBusinessLocation
        {
            Label = Label,
            Area = Area,
            City = City,
            Province = Province,
            Latitude = Latitude,
            Longitude = Longitude,
            Source = Source,
            Precision = Precision,
            IsResolved = IsResolved
        };
    }
}
