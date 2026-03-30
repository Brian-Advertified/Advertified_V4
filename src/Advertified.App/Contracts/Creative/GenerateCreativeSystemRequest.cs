namespace Advertified.App.Contracts.Creative;

public sealed class GenerateCreativeSystemRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? IterationLabel { get; set; }
    public string? Brand { get; set; }
    public string? Product { get; set; }
    public string? Audience { get; set; }
    public string? Objective { get; set; }
    public string? Tone { get; set; }
    public IReadOnlyList<string> Channels { get; set; } = Array.Empty<string>();
    public string? Cta { get; set; }
    public IReadOnlyList<string> Constraints { get; set; } = Array.Empty<string>();
}
