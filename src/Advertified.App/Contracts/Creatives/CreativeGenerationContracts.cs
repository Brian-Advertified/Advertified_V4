namespace Advertified.App.Contracts.Creatives;

public sealed class GenerateCreativesRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public CreativeBusinessRequest Business { get; set; } = new();
    public string Objective { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public CreativeAudienceRequest Audience { get; set; } = new();
    public IReadOnlyList<string> Channels { get; set; } = Array.Empty<string>();
    public string Tone { get; set; } = string.Empty;
}

public sealed class CreativeBusinessRequest
{
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public sealed class CreativeAudienceRequest
{
    public string Lsm { get; set; } = string.Empty;
    public string AgeRange { get; set; } = string.Empty;
    public IReadOnlyList<string> Languages { get; set; } = Array.Empty<string>();
}

public sealed class RegenerateCreativeRequest
{
    public Guid CreativeId { get; set; }
    public string Feedback { get; set; } = string.Empty;
}

public sealed class GenerateCreativesResponse
{
    public string CampaignId { get; set; } = string.Empty;
    public GeneratedCreativesByChannelResponse Creatives { get; set; } = new();
    public CreativeScoresResponse Scores { get; set; } = new();
    public CreativeGenerationMetadataResponse Metadata { get; set; } = new();
}

public sealed class GeneratedCreativesByChannelResponse
{
    public IReadOnlyList<RadioCreativeResponse> Radio { get; set; } = Array.Empty<RadioCreativeResponse>();
    public IReadOnlyList<TvCreativeResponse> Tv { get; set; } = Array.Empty<TvCreativeResponse>();
    public IReadOnlyList<BillboardCreativeResponse> Billboard { get; set; } = Array.Empty<BillboardCreativeResponse>();
    public IReadOnlyList<NewspaperCreativeResponse> Newspaper { get; set; } = Array.Empty<NewspaperCreativeResponse>();
    public IReadOnlyList<DigitalCreativeResponse> Digital { get; set; } = Array.Empty<DigitalCreativeResponse>();
}

public sealed class RadioCreativeResponse
{
    public string Id { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public int Duration { get; set; } = 30;
    public string Script { get; set; } = string.Empty;
    public string VoiceTone { get; set; } = string.Empty;
    public string Cta { get; set; } = string.Empty;
    public string Format { get; set; } = "Dialogue";
}

public sealed class TvCreativeResponse
{
    public string Id { get; set; } = string.Empty;
    public int Duration { get; set; } = 30;
    public IReadOnlyList<TvSceneResponse> Scenes { get; set; } = Array.Empty<TvSceneResponse>();
    public string Cta { get; set; } = string.Empty;
}

public sealed class TvSceneResponse
{
    public int Scene { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Dialogue { get; set; } = string.Empty;
}

public sealed class BillboardCreativeResponse
{
    public string Id { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Subtext { get; set; } = string.Empty;
    public string Cta { get; set; } = string.Empty;
    public string VisualDirection { get; set; } = string.Empty;
}

public sealed class NewspaperCreativeResponse
{
    public string Id { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Cta { get; set; } = string.Empty;
}

public sealed class DigitalCreativeResponse
{
    public string Id { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string PrimaryText { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Cta { get; set; } = string.Empty;
    public int Variants { get; set; } = 3;
    public string? Hook { get; set; }
    public string? Script { get; set; }
    public int? Duration { get; set; }
}

public sealed class CreativeScoresResponse
{
    public IReadOnlyList<CreativeChannelScoreResponse> Radio { get; set; } = Array.Empty<CreativeChannelScoreResponse>();
    public IReadOnlyList<CreativeChannelScoreResponse> Tv { get; set; } = Array.Empty<CreativeChannelScoreResponse>();
    public IReadOnlyList<CreativeChannelScoreResponse> Billboard { get; set; } = Array.Empty<CreativeChannelScoreResponse>();
    public IReadOnlyList<CreativeChannelScoreResponse> Newspaper { get; set; } = Array.Empty<CreativeChannelScoreResponse>();
    public IReadOnlyList<CreativeChannelScoreResponse> Digital { get; set; } = Array.Empty<CreativeChannelScoreResponse>();
}

public sealed class CreativeChannelScoreResponse
{
    public string CreativeId { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, decimal> Metrics { get; set; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
}

public sealed class CreativeGenerationMetadataResponse
{
    public string RunId { get; set; } = string.Empty;
    public string BriefVersion { get; set; } = "v1";
    public string GeneratorVersion { get; set; } = "v1";
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class CreativeBriefResponse
{
    public string Brand { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Tone { get; set; } = string.Empty;
    public string KeyMessage { get; set; } = string.Empty;
    public string Cta { get; set; } = string.Empty;
    public IReadOnlyList<string> Languages { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AudienceInsights { get; set; } = Array.Empty<string>();
}

public sealed class LocalisationRequest
{
    public string BaseLanguage { get; set; } = "English";
    public string TargetLanguage { get; set; } = "English";
    public string Tone { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class LocalisationResponse
{
    public string BaseLanguage { get; set; } = "English";
    public string TargetLanguage { get; set; } = "English";
    public string AdaptedContent { get; set; } = string.Empty;
    public IReadOnlyList<string> Notes { get; set; } = Array.Empty<string>();
}

public sealed class CreativeScoreRequest
{
    public string Channel { get; set; } = string.Empty;
    public string CreativeId { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> Signals { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
