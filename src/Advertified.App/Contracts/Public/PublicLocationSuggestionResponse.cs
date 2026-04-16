namespace Advertified.App.Contracts.Public;

public sealed class PublicLocationSuggestionResponse
{
    public string Label { get; set; } = string.Empty;

    public string LocationType { get; set; } = string.Empty;

    public string? City { get; set; }

    public string? Province { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string Source { get; set; } = string.Empty;
}
