namespace Advertified.App.Contracts.Leads;

public sealed class LeadConfidenceGateDto
{
    public bool IsBlocked { get; init; }

    public IReadOnlyList<string> RequiredFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> MissingRequiredFields { get; init; } = Array.Empty<string>();

    public string Message { get; init; } = string.Empty;
}
