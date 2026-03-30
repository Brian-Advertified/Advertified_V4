namespace Advertified.App.Contracts.Admin;

public sealed class CreateAdminGeographyRequest
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> FallbackLocations { get; set; } = Array.Empty<string>();
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class UpdateAdminGeographyRequest
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> FallbackLocations { get; set; } = Array.Empty<string>();
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class UpsertAdminGeographyMappingRequest
{
    public string? Province { get; set; }
    public string? City { get; set; }
    public string? StationOrChannelName { get; set; }
}
