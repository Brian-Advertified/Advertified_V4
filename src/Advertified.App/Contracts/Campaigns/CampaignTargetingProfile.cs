namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignTargetingProfile
{
    public string Scope { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Source { get; set; } = "none";
    public string Precision { get; set; } = "unknown";
    public List<string> Provinces { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public List<string> Suburbs { get; set; } = new();
    public List<string> Areas { get; set; } = new();
    public List<string> PriorityAreas { get; set; } = new();
    public List<string> Exclusions { get; set; } = new();

    public CampaignTargetingProfile DeepClone()
    {
        return new CampaignTargetingProfile
        {
            Scope = Scope,
            Label = Label,
            City = City,
            Province = Province,
            Latitude = Latitude,
            Longitude = Longitude,
            Source = Source,
            Precision = Precision,
            Provinces = Provinces.ToList(),
            Cities = Cities.ToList(),
            Suburbs = Suburbs.ToList(),
            Areas = Areas.ToList(),
            PriorityAreas = PriorityAreas.ToList(),
            Exclusions = Exclusions.ToList()
        };
    }
}
