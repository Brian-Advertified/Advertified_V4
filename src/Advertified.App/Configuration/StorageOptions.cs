namespace Advertified.App.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string? PublicBucket { get; set; }

    public string? PrivateBucket { get; set; }

    public string? ServiceUrl { get; set; }

    public string? Region { get; set; }

    public string? PublicBaseUrl { get; set; }

    public string? AccessKeyId { get; set; }

    public string? SecretAccessKey { get; set; }

    public bool ForcePathStyle { get; set; }
}
