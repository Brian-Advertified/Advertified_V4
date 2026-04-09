using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Agent;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class CampaignBriefInterpretationService : ICampaignBriefInterpretationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAIOptions _options;
    private readonly ILogger<CampaignBriefInterpretationService> _logger;
    private readonly IBroadcastMasterDataService _broadcastMasterDataService;

    public CampaignBriefInterpretationService(
        HttpClient httpClient,
        IOptions<OpenAIOptions> options,
        ILogger<CampaignBriefInterpretationService> logger,
        IBroadcastMasterDataService broadcastMasterDataService)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _broadcastMasterDataService = broadcastMasterDataService;
    }

    public async Task<InterpretedCampaignBriefResponse> InterpretAsync(
        InterpretCampaignBriefRequest request,
        CancellationToken cancellationToken)
    {
        if (_options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var aiResult = await TryInterpretWithOpenAIAsync(request, cancellationToken);
            if (aiResult is not null)
            {
                return aiResult;
            }
        }

        return InterpretHeuristically(request);
    }

    private async Task<InterpretedCampaignBriefResponse?> TryInterpretWithOpenAIAsync(
        InterpretCampaignBriefRequest request,
        CancellationToken cancellationToken)
    {
        var masterData = await _broadcastMasterDataService.GetOutletMasterDataAsync(cancellationToken);
        var supportedProvinceCodes = masterData.Provinces
            .Select(option => option.Value?.Trim().ToLowerInvariant())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var geographyHint = supportedProvinceCodes.Length > 0
            ? string.Join("|", supportedProvinceCodes)
            : "national";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Content = JsonContent.Create(
            new OpenAIChatCompletionRequest
            {
                Model = _options.Model,
                ResponseFormat = new OpenAIResponseFormat { Type = "json_object" },
                Messages = new[]
                {
                    new OpenAIChatMessage
                    {
                        Role = "system",
                        Content =
                            "You are Advertified's campaign brief interpreter.\n" +
                            "Convert a messy campaign note into structured planning inputs.\n" +
                            "Return strict JSON with this shape:\n" +
                            "{\n" +
                            "  \"objective\": \"awareness|launch|promotion|brand_presence|leads\",\n" +
                            "  \"audience\": \"mass-market|youth|business|retail\",\n" +
                            "  \"scope\": \"local|regional|national\",\n" +
                            $"  \"geography\": \"{geographyHint}\",\n" +
                            "  \"tone\": \"premium|balanced|high-visibility|performance\",\n" +
                            "  \"campaignName\": \"string\",\n" +
                            "  \"channels\": [\"Radio\", \"Billboards and Digital Screens\", \"TV\"],\n" +
                            "  \"summary\": \"short plain-language explanation\"\n" +
                            "}\n" +
                            "Never return channels outside Radio, Billboards and Digital Screens, TV."
                    },
                    new OpenAIChatMessage
                    {
                        Role = "user",
                        Content =
                            "Interpret this campaign brief.\n" +
                            $"Budget: {request.SelectedBudget}\n" +
                            $"Campaign name: {request.CampaignName ?? "Not provided"}\n" +
                            $"Brief: {request.Brief}"
                    }
                }
            },
            mediaType: null,
            options: JsonOptions);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("OpenAI brief interpretation failed. Status {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
            return null;
        }

        var completion = await response.Content.ReadFromJsonAsync<OpenAIChatCompletionResponse>(cancellationToken: cancellationToken);
        var rawContent = completion?.Choices?
            .Select(choice => choice.Message?.Content)
            .FirstOrDefault(content => !string.IsNullOrWhiteSpace(content));

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<InterpretedCampaignBriefResponse>(rawContent, JsonOptions);
            if (result is null)
            {
                return null;
            }

            result.Objective = NormalizeObjective(result.Objective);
            result.Audience = NormalizeAudience(result.Audience);
            result.Scope = NormalizeScope(result.Scope);
            result.Geography = NormalizeGeography(result.Geography, supportedProvinceCodes, _broadcastMasterDataService.NormalizeProvinceCode);
            result.Tone = NormalizeTone(result.Tone);
            result.Channels = NormalizeChannels(result.Channels);
            result.CampaignName = string.IsNullOrWhiteSpace(result.CampaignName)
                ? request.CampaignName ?? "Campaign recommendation"
                : result.CampaignName.Trim();
            result.Summary = string.IsNullOrWhiteSpace(result.Summary)
                ? "The system interpreted the brief and suggested a structured planning starting point."
                : result.Summary.Trim()
                    .Replace("OOH", "Billboards and Digital Screens", StringComparison.OrdinalIgnoreCase);

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OpenAI brief interpretation response could not be parsed.");
            return null;
        }
    }

    private InterpretedCampaignBriefResponse InterpretHeuristically(InterpretCampaignBriefRequest request)
    {
        var brief = request.Brief ?? string.Empty;
        var lower = brief.ToLowerInvariant();
        var masterData = _broadcastMasterDataService.GetOutletMasterDataAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var supportedProvinceCodes = masterData.Provinces
            .Select(option => option.Value?.Trim().ToLowerInvariant())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var objective = lower.Contains("launch") ? "launch"
            : lower.Contains("promo") || lower.Contains("promotion") || lower.Contains("sale") ? "promotion"
            : lower.Contains("lead") ? "leads"
            : lower.Contains("brand") ? "brand_presence"
            : "awareness";

        var audience = lower.Contains("shopper") || lower.Contains("retail") ? "retail"
            : lower.Contains("youth") ? "youth"
            : lower.Contains("business") || lower.Contains("professional") ? "business"
            : "mass-market";

        var scope = request.SelectedBudget >= 1000000m || lower.Contains("national") ? "national"
            : request.SelectedBudget >= 500000m ? "regional"
            : "local";

        var geography = ResolveHeuristicGeography(
            lower,
            masterData.Provinces,
            supportedProvinceCodes,
            _broadcastMasterDataService.NormalizeProvinceCode);

        var channels = new List<string>();
        if (lower.Contains("radio")) channels.Add("Radio");
        if (lower.Contains("billboard") || lower.Contains("outdoor") || lower.Contains("ooh")) channels.Add("OOH");
        if (lower.Contains("tv") || lower.Contains("television")) channels.Add("TV");
        if (channels.Count == 0)
        {
            channels.AddRange(new[] { "Radio", "OOH" });
        }

        var tone = lower.Contains("premium") ? "premium"
            : lower.Contains("performance") ? "performance"
            : lower.Contains("visibility") || lower.Contains("high impact") ? "high-visibility"
            : "balanced";

        return new InterpretedCampaignBriefResponse
        {
            Objective = objective,
            Audience = audience,
            Scope = scope,
            Geography = NormalizeGeography(geography, supportedProvinceCodes, _broadcastMasterDataService.NormalizeProvinceCode),
            Tone = tone,
            CampaignName = string.IsNullOrWhiteSpace(request.CampaignName) ? "Campaign recommendation" : request.CampaignName.Trim(),
            Channels = channels,
            Summary = "The brief was interpreted into a structured planning setup that the agent can refine before generating the draft."
        };
    }

    private static string NormalizeObjective(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "launch" or "promotion" or "brand_presence" or "leads" ? normalized : "awareness";
    }

    private static string NormalizeAudience(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "youth" or "business" or "retail" ? normalized : "mass-market";
    }

    private static string NormalizeScope(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "local" or "national" ? normalized : "regional";
    }

    private static string NormalizeGeography(
        string? value,
        IReadOnlyCollection<string> supportedProvinceCodes,
        Func<string?, string> normalizeProvinceCode)
    {
        var normalized = normalizeProvinceCode(value).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ResolveDefaultGeography(supportedProvinceCodes);
        }

        if (supportedProvinceCodes.Count == 0)
        {
            return normalized;
        }

        return supportedProvinceCodes.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? normalized
            : ResolveDefaultGeography(supportedProvinceCodes);
    }

    private static string NormalizeTone(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "premium" or "high-visibility" or "performance" ? normalized : "balanced";
    }

    private static IReadOnlyList<string> NormalizeChannels(IReadOnlyList<string>? channels)
    {
        if (channels is null || channels.Count == 0)
        {
            return new[] { "Radio", "OOH" };
        }

        var normalized = channels
            .Select(channel => channel.Trim().ToLowerInvariant())
            .Select(channel => channel switch
            {
                "radio" => "Radio",
                "ooh" => "OOH",
                "Billboards and Digital Screens" => "OOH",
                "billboards" => "OOH",
                "outdoor" => "OOH",
                "tv" => "TV",
                _ => null
            })
            .Where(channel => channel is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? new[] { "Radio", "OOH" } : normalized;
    }

    private static string ResolveHeuristicGeography(
        string lowerBrief,
        IReadOnlyList<Advertified.App.Contracts.Admin.AdminLookupOptionResponse> provinces,
        IReadOnlyCollection<string> supportedProvinceCodes,
        Func<string?, string> normalizeProvinceCode)
    {
        foreach (var province in provinces)
        {
            var label = (province.Label ?? string.Empty).Trim().ToLowerInvariant();
            var value = (province.Value ?? string.Empty).Trim().ToLowerInvariant();
            if ((!string.IsNullOrWhiteSpace(label) && lowerBrief.Contains(label, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(value) && lowerBrief.Contains(value.Replace('_', ' '), StringComparison.OrdinalIgnoreCase)))
            {
                return value;
            }
        }

        var normalizedText = new string(lowerBrief
            .Select(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) ? character : ' ')
            .ToArray());
        var tokens = normalizedText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var resolved = normalizeProvinceCode(token).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(resolved))
            {
                continue;
            }

            if (supportedProvinceCodes.Count == 0 || supportedProvinceCodes.Contains(resolved, StringComparer.OrdinalIgnoreCase))
            {
                return resolved;
            }
        }

        for (var index = 0; index < tokens.Length - 1; index++)
        {
            var compound = $"{tokens[index]} {tokens[index + 1]}";
            var resolved = normalizeProvinceCode(compound).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(resolved))
            {
                continue;
            }

            if (supportedProvinceCodes.Count == 0 || supportedProvinceCodes.Contains(resolved, StringComparer.OrdinalIgnoreCase))
            {
                return resolved;
            }
        }

        if (lowerBrief.Contains("national", StringComparison.OrdinalIgnoreCase)
            && supportedProvinceCodes.Contains("national", StringComparer.OrdinalIgnoreCase))
        {
            return "national";
        }

        return ResolveDefaultGeography(supportedProvinceCodes);
    }

    private static string ResolveDefaultGeography(IReadOnlyCollection<string> supportedProvinceCodes)
    {
        if (supportedProvinceCodes.Contains("national", StringComparer.OrdinalIgnoreCase))
        {
            return "national";
        }

        return supportedProvinceCodes.FirstOrDefault() ?? string.Empty;
    }

    private sealed class OpenAIChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("response_format")]
        public OpenAIResponseFormat ResponseFormat { get; set; } = new();

        [JsonPropertyName("messages")]
        public IReadOnlyList<OpenAIChatMessage> Messages { get; set; } = Array.Empty<OpenAIChatMessage>();
    }

    private sealed class OpenAIResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "json_object";
    }

    private sealed class OpenAIChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OpenAIChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public IReadOnlyList<OpenAIChoice>? Choices { get; set; }
    }

    private sealed class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public OpenAIChatMessage? Message { get; set; }
    }
}
