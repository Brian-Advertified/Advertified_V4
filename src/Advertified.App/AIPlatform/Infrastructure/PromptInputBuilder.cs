using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class PromptInputBuilder : IPromptInputBuilder
{
    public IReadOnlyDictionary<string, string> BuildVariables(
        CreativeBrief brief,
        AdvertisingChannel channel,
        string language,
        string templateKey)
    {
        var audience = brief.AudienceInsights.Count > 0
            ? string.Join(", ", brief.AudienceInsights)
            : "General audience";

        var location = brief.AudienceInsights.FirstOrDefault(item => item.Contains("South Africa", StringComparison.OrdinalIgnoreCase))
            ?? brief.AudienceInsights.LastOrDefault()
            ?? "South Africa";

        // Variables are standardized so prompt templates can be swapped/versioned without changing engine logic.
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["brandName"] = brief.Brand,
            ["objective"] = brief.Objective,
            ["audience"] = audience,
            ["tone"] = brief.Tone,
            ["language"] = language,
            ["keyMessage"] = brief.KeyMessage,
            ["cta"] = brief.CallToAction,
            ["offer"] = brief.KeyMessage,
            ["message"] = brief.KeyMessage,
            ["content"] = brief.KeyMessage,
            ["feedback"] = "Improve clarity and urgency while preserving brand tone.",
            ["creative"] = brief.KeyMessage,
            ["location"] = location,
            ["style"] = "Bold",
            ["format"] = channel == AdvertisingChannel.Radio ? "SingleVoice" : "Standard",
            ["duration"] = channel is AdvertisingChannel.Radio or AdvertisingChannel.Tv ? "30" : "15",
            ["templateKey"] = templateKey
        };
    }
}
