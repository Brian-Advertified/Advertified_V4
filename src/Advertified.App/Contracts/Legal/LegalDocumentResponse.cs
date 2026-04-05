namespace Advertified.App.Contracts.Legal;

public sealed class LegalDocumentResponse
{
    public string DocumentKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionLabel { get; init; } = string.Empty;
    public IReadOnlyList<LegalDocumentSectionResponse> Sections { get; init; } = Array.Empty<LegalDocumentSectionResponse>();
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class LegalDocumentSectionResponse
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Paragraphs { get; init; } = Array.Empty<string>();
}
