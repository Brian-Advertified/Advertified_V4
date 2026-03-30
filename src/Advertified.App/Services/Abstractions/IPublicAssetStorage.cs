namespace Advertified.App.Services.Abstractions;

public interface IPublicAssetStorage
{
    Task<string> SaveAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken);

    Task<byte[]> GetBytesAsync(string objectKey, CancellationToken cancellationToken);

    string? GetPublicUrl(string objectKey);
}
