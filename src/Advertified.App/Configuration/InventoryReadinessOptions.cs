namespace Advertified.App.Configuration;

public sealed class InventoryReadinessOptions
{
    public const string SectionName = "InventoryReadiness";

    public bool EnforceOnStartup { get; set; } = true;
    public int MinimumOohInventoryRows { get; set; } = 1;
}
