using Advertified.App.AIPlatform.Domain;

namespace Advertified.App.AIPlatform.Api;

public sealed class CreativeEngineGenerateRequest
{
    public Guid CampaignId { get; set; }
    public string? PromptOverride { get; set; }
    public Guid? VoicePackId { get; set; }
    public bool PersistOutputs { get; set; } = true;
    public string? IdempotencyKey { get; set; }
}

public sealed class CreativeBriefInputDto
{
    public Guid CampaignId { get; set; }
    public decimal Budget { get; set; } = 50000m;
    public string Brand { get; set; } = string.Empty;
    public string Objective { get; set; } = "Awareness";
    public string Tone { get; set; } = "Balanced";
    public string KeyMessage { get; set; } = string.Empty;
    public string CallToAction { get; set; } = "Get started today";
    public List<string> AudienceInsights { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public List<AdvertisingChannel> Channels { get; set; } = new();
    public int PromptVersion { get; set; } = 1;
    public int MaxVariantsPerChannel { get; set; } = 2;
}

public sealed class CreativeEngineGenerateFromBriefRequest
{
    public CreativeBriefInputDto Brief { get; set; } = new();
}

public sealed class CreativeEngineCreativeItemResponse
{
    public Guid CreativeId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}

public sealed class CreativeEngineGenerateResponse
{
    public Guid CampaignId { get; set; }
    public Guid JobId { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public List<CreativeEngineCreativeItemResponse> Creatives { get; set; } = new();
}
