namespace Advertified.App.Services.Abstractions;

public interface IPrivateDocumentStorage
{
    Task<string> SaveAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken);

    Task<byte[]> GetBytesAsync(string objectKey, CancellationToken cancellationToken);
}
