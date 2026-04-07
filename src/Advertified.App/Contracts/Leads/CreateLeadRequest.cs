namespace Advertified.App.Contracts.Leads;

public sealed class CreateLeadRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Website { get; init; }

    public string Location { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Source { get; init; } = "manual";

    public string? SourceReference { get; init; }
}
