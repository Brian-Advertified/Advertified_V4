namespace Advertified.App.Configuration;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    public string Model { get; set; } = "gpt-5-mini";

    public int TimeoutSeconds { get; set; } = 30;
}
