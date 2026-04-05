using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data.Entities;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;
using CampaignEntity = Advertified.App.Data.Entities.Campaign;
using CampaignBriefEntity = Advertified.App.Data.Entities.CampaignBrief;

namespace Advertified.App.Services;

public sealed class OpenAICampaignReasoningService : ICampaignReasoningService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly object QuotaBackoffGate = new();
    private static readonly TimeSpan QuotaBackoffDuration = TimeSpan.FromMinutes(30);
    private static DateTimeOffset? _quotaBackoffUntilUtc;

    private readonly HttpClient _httpClient;
    private readonly OpenAIOptions _options;
    private readonly ILogger<OpenAICampaignReasoningService> _logger;

    public OpenAICampaignReasoningService(
        HttpClient httpClient,
        IOptions<OpenAIOptions> options,
        ILogger<OpenAICampaignReasoningService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CampaignReasoningResult?> GenerateAsync(
        CampaignEntity campaign,
        CampaignBriefEntity brief,
        CampaignPlanningRequest planningRequest,
        RecommendationResult recommendationResult,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return null;
        }

        var quotaBackoffUntilUtc = GetQuotaBackoffUntilUtc();
        if (quotaBackoffUntilUtc.HasValue && quotaBackoffUntilUtc.Value > DateTimeOffset.UtcNow)
        {
            _logger.LogWarning(
                "Skipping OpenAI reasoning request because insufficient quota backoff is active until {BackoffUntilUtc}.",
                quotaBackoffUntilUtc.Value);
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = JsonContent.Create(
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
                            "You are Advertified's campaign strategy reasoning layer.\n" +
                            "You help explain and interpret a deterministic media recommendation.\n" +
                            "You must stay grounded in the supplied campaign brief, package rules, selected items, and fallback flags.\n" +
                            "Do not invent inventory, reach, audience sizes, or performance metrics.\n" +
                            "Return strict JSON with this shape:\n" +
                            "{\n" +
                            "  \"summary\": \"1-2 sentence summary\",\n" +
                            "  \"rationale\": \"A concise explanation of why this recommendation makes sense and any cautions the agent should know.\"\n" +
                            "}"
                    },
                    new OpenAIChatMessage
                    {
                        Role = "user",
                        Content = BuildPrompt(campaign, brief, planningRequest, recommendationResult)
                    }
                }
            },
            mediaType: null,
            options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            TryActivateQuotaBackoff(response.StatusCode, responseBody);
            _logger.LogWarning(
                "OpenAI reasoning request failed. Status: {StatusCode}. Body: {Body}",
                (int)response.StatusCode,
                responseBody);
            return null;
        }

        var completion = await response.Content.ReadFromJsonAsync<OpenAIChatCompletionResponse>(JsonOptions, cancellationToken);
        var rawContent = completion?.Choices?
            .Select(choice => choice.Message?.Content)
            .FirstOrDefault(content => !string.IsNullOrWhiteSpace(content));

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<CampaignReasoningResult>(rawContent, JsonOptions);
            if (result is null || string.IsNullOrWhiteSpace(result.Summary) || string.IsNullOrWhiteSpace(result.Rationale))
            {
                return null;
            }

            result.Summary = result.Summary.Trim();
            result.Rationale = result.Rationale.Trim();
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OpenAI reasoning response could not be parsed as JSON.");
            return null;
        }
    }

    private static string BuildPrompt(
        CampaignEntity campaign,
        CampaignBriefEntity brief,
        CampaignPlanningRequest planningRequest,
        RecommendationResult recommendationResult)
    {
        var packageName = campaign.PackageBand?.Name ?? "Unknown package";
        var campaignName = string.IsNullOrWhiteSpace(campaign.CampaignName)
            ? $"{packageName} campaign"
            : campaign.CampaignName.Trim();
        var preferredChannels = planningRequest.PreferredMediaTypes.Count > 0
            ? string.Join(", ", planningRequest.PreferredMediaTypes)
            : "Not specified";
        var targetLanguages = planningRequest.TargetLanguages.Count > 0
            ? string.Join(", ", planningRequest.TargetLanguages)
            : "Not specified";
        var provinces = planningRequest.Provinces.Count > 0
            ? string.Join(", ", planningRequest.Provinces)
            : "Not specified";
        var notes = string.Join(
            " | ",
            new[]
            {
                brief.TargetAudienceNotes,
                brief.CreativeNotes,
                brief.SpecialRequirements
            }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));

        var plannedItems = recommendationResult.RecommendedPlan
            .Take(12)
            .Select(item =>
            {
                var reasons = TryGetStringArray(item.Metadata, "selectionReasons");
                var confidence = TryGetDecimal(item.Metadata, "confidenceScore");
                var line = new StringBuilder()
                    .Append("- ")
                    .Append(item.DisplayName)
                    .Append(" | ")
                    .Append(item.MediaType)
                    .Append(" | R ")
                    .Append(item.TotalCost.ToString("N0"));

                if (reasons.Count > 0)
                {
                    line.Append(" | reasons: ").Append(string.Join(", ", reasons.Take(3)));
                }

                if (confidence.HasValue)
                {
                    line.Append(" | confidence: ").Append(confidence.Value.ToString("0.00"));
                }

                return line.ToString();
            });

        return
            "Explain this Advertified recommendation for an internal agent review workflow.\n\n" +
            "Campaign:\n" +
            $"- Campaign name: {campaignName}\n" +
            $"- Package: {packageName}\n" +
            $"- Budget: R {planningRequest.SelectedBudget:N0}\n" +
            $"- Objective: {brief.Objective}\n" +
            $"- Geography scope: {brief.GeographyScope}\n" +
            $"- Provinces: {provinces}\n" +
            $"- Preferred channels: {preferredChannels}\n" +
            $"- Target languages: {targetLanguages}\n" +
            $"- Open to upsell: {brief.OpenToUpsell}\n" +
            $"- Additional budget: {(brief.AdditionalBudget.HasValue ? $"R {brief.AdditionalBudget.Value:N0}" : "None")}\n" +
            $"- Audience notes: {(string.IsNullOrWhiteSpace(notes) ? "Not specified" : notes)}\n\n" +
            "Deterministic planner summary:\n" +
            $"- Recommended items: {recommendationResult.RecommendedPlan.Count}\n" +
            $"- Recommended total: R {recommendationResult.RecommendedPlanTotal:N0}\n" +
            $"- Manual review required: {recommendationResult.ManualReviewRequired}\n" +
            $"- Fallback flags: {(recommendationResult.FallbackFlags.Count > 0 ? string.Join(", ", recommendationResult.FallbackFlags) : "None")}\n\n" +
            "Recommended items:\n" +
            string.Join(Environment.NewLine, plannedItems) +
            "\n\nWrite:\n" +
            "1. A concise recommendation summary.\n" +
            "2. A grounded rationale that explains the strategic fit and clearly mentions any caution if fallback flags or manual review are present.";
    }

    private static IReadOnlyList<string> TryGetStringArray(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return Array.Empty<string>();
        }

        return value switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.Array => element
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToArray(),
            IEnumerable<object?> items => items
                .Select(item => item?.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToArray(),
            _ => Array.Empty<string>()
        };
    }

    private static decimal? TryGetDecimal(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            decimal decimalValue => decimalValue,
            double doubleValue => Convert.ToDecimal(doubleValue),
            float floatValue => Convert.ToDecimal(floatValue),
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var parsed) => parsed,
            string text when decimal.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private static DateTimeOffset? GetQuotaBackoffUntilUtc()
    {
        lock (QuotaBackoffGate)
        {
            return _quotaBackoffUntilUtc;
        }
    }

    private static void TryActivateQuotaBackoff(System.Net.HttpStatusCode statusCode, string? responseBody)
    {
        if (statusCode != System.Net.HttpStatusCode.TooManyRequests
            || string.IsNullOrWhiteSpace(responseBody)
            || !responseBody.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lock (QuotaBackoffGate)
        {
            _quotaBackoffUntilUtc = DateTimeOffset.UtcNow.Add(QuotaBackoffDuration);
        }
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
