namespace Advertified.App.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string? PublicBucket { get; set; }

    public string? Endpoint { get; set; }

    public string? Region { get; set; }

    public string? PublicBaseUrl { get; set; }

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }
}
