using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advertified.App.Configuration;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class InsightService : IInsightService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAIOptions _options;
    private readonly ILogger<InsightService> _logger;

    public InsightService(
        HttpClient httpClient,
        IOptions<OpenAIOptions> options,
        ILogger<InsightService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateInsightAsync(
        Lead lead,
        Signal? previousSignal,
        Signal currentSignal,
        LeadScoreResult score,
        LeadTrendAnalysisResult trend,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return BuildFallbackInsight(lead, previousSignal, currentSignal, score, trend);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Content = JsonContent.Create(
                new InsightChatCompletionRequest
                {
                    Model = _options.Model,
                    Messages = new[]
                    {
                        new InsightChatMessage
                        {
                            Role = "system",
                            Content =
                                "You are Advertified's lead intelligence assistant.\n" +
                                "Return a concise 2-3 sentence business insight.\n" +
                                "Stay grounded in the provided business details, detected signals, score, and change over time.\n" +
                                "Do not invent facts, metrics, or channels that were not provided.\n" +
                                "Use confidence-safe wording: say 'we found evidence of' or 'we did not find strong evidence of' instead of absolute claims.\n" +
                                "Keep the output short, clear, and practical."
                        },
                        new InsightChatMessage
                        {
                            Role = "user",
                            Content = BuildPrompt(lead, previousSignal, currentSignal, score, trend)
                        }
                    },
                    MaxTokens = 120,
                    Temperature = 0.4m
                },
                mediaType: null,
                options: JsonOptions);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Insight OpenAI request failed. Status: {StatusCode}. Body: {Body}",
                    (int)response.StatusCode,
                    responseBody);
                return BuildFallbackInsight(lead, previousSignal, currentSignal, score, trend);
            }

            var completion = await response.Content.ReadFromJsonAsync<InsightChatCompletionResponse>(JsonOptions, cancellationToken);
            var content = completion?.Choices?
                .Select(choice => choice.Message?.Content?.Trim())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            return string.IsNullOrWhiteSpace(content)
                ? BuildFallbackInsight(lead, previousSignal, currentSignal, score, trend)
                : content;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Insight generation failed. Falling back to rule-based insight.");
            return BuildFallbackInsight(lead, previousSignal, currentSignal, score, trend);
        }
    }

    internal static string BuildPrompt(
        Lead lead,
        Signal? previousSignal,
        Signal currentSignal,
        LeadScoreResult score,
        LeadTrendAnalysisResult trend)
    {
        return
            "Create a short Advertified business insight.\n\n" +
            "Business:\n" +
            $"- Name: {lead.Name}\n" +
            $"- Website: {DisplayValue(lead.Website)}\n" +
            $"- Location: {lead.Location}\n" +
            $"- Category: {lead.Category}\n\n" +
            "Current signals detected:\n" +
            $"- Has promo: {currentSignal.HasPromo}\n" +
            $"- Has Meta ads: {currentSignal.HasMetaAds}\n" +
            $"- Website updated recently: {currentSignal.WebsiteUpdatedRecently}\n\n" +
            "Previous signals detected:\n" +
            $"- Has promo: {FormatHistoricalBoolean(previousSignal?.HasPromo)}\n" +
            $"- Has Meta ads: {FormatHistoricalBoolean(previousSignal?.HasMetaAds)}\n" +
            $"- Website updated recently: {FormatHistoricalBoolean(previousSignal?.WebsiteUpdatedRecently)}\n\n" +
            "Trend analysis:\n" +
            $"- Summary: {trend.Summary}\n" +
            $"- Campaign started recently: {trend.CampaignStartedRecently}\n" +
            $"- Activity increased: {trend.ActivityIncreased}\n\n" +
            "Scoring:\n" +
            $"- Score: {score.Score}\n" +
            $"- Intent level: {score.IntentLevel}\n\n" +
            "Instructions:\n" +
            "- Write 2-3 sentences only.\n" +
            "- Mention what changed over time.\n" +
            "- Mention likely marketing behavior.\n" +
            "- Mention any obvious gap or opportunity.\n" +
            "- Use cautious wording for uncertain signals (e.g., 'we found evidence of', 'we did not find strong evidence of').\n" +
            "- Do not use bullet points.\n" +
            "- Do not exceed 60 words.";
    }

    internal static string BuildFallbackInsight(
        Lead lead,
        Signal? previousSignal,
        Signal currentSignal,
        LeadScoreResult score,
        LeadTrendAnalysisResult trend)
    {
        var activity = new List<string>();
        if (currentSignal.HasPromo)
        {
            activity.Add("actively running promotions");
        }

        if (currentSignal.HasMetaAds)
        {
            activity.Add("showing website patterns that can align with Meta advertising");
        }

        if (currentSignal.WebsiteUpdatedRecently)
        {
            activity.Add("keeping its website updated");
        }

        var firstSentence = activity.Count > 0
            ? $"We found evidence that {lead.Name} is {JoinPhrases(activity)}."
            : $"We did not find strong evidence of recent digital campaign activity for {lead.Name}.";

        var secondSentence = score.IntentLevel switch
        {
            "High" => "The lead shows high buying intent and is a strong candidate for immediate outreach.",
            "Medium" => "The lead shows moderate intent and may respond well to targeted outreach.",
            _ => "The lead shows lower intent and may need a lighter-touch nurture approach."
        };

        var opportunity = currentSignal.HasMetaAds
            ? "There appears to be an opportunity to strengthen search capture and broaden awareness channels."
            : "There appears to be an opportunity to improve paid demand capture and broader campaign coverage.";

        var trendSentence = previousSignal is null
            ? "This is the first intelligence snapshot for the lead."
            : trend.Summary;

        return $"{firstSentence} {trendSentence} {secondSentence} {opportunity}";
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not provided" : value.Trim();
    }

    private static string JoinPhrases(IReadOnlyList<string> phrases)
    {
        return phrases.Count switch
        {
            0 => string.Empty,
            1 => phrases[0],
            2 => $"{phrases[0]} and {phrases[1]}",
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))}, and {phrases[^1]}"
        };
    }

    private static string FormatHistoricalBoolean(bool? value)
    {
        return value.HasValue ? value.Value.ToString() : "Unknown";
    }

    private sealed class InsightChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("messages")]
        public IReadOnlyList<InsightChatMessage> Messages { get; init; } = Array.Empty<InsightChatMessage>();

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; }

        [JsonPropertyName("temperature")]
        public decimal Temperature { get; init; }
    }

    private sealed class InsightChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;
    }

    private sealed class InsightChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<InsightChatChoice>? Choices { get; init; }
    }

    private sealed class InsightChatChoice
    {
        [JsonPropertyName("message")]
        public InsightChatMessage? Message { get; init; }
    }
}
