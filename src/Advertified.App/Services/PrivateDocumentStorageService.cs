using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class PrivateDocumentStorageService : IPrivateDocumentStorage
{
    private readonly HttpClient _httpClient;
    private readonly StorageOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PrivateDocumentStorageService> _logger;

    public PrivateDocumentStorageService(
        HttpClient httpClient,
        IOptions<StorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<PrivateDocumentStorageService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SaveAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeKey(objectKey);
        if (UseS3())
        {
            await UploadToS3Async(normalizedKey, content, contentType, cancellationToken);
            return normalizedKey;
        }

        var absolutePath = ResolveLocalPath(normalizedKey);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(absolutePath, content, cancellationToken);
        return normalizedKey;
    }

    public async Task<byte[]> GetBytesAsync(string objectKey, CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeKey(objectKey);
        if (UseS3())
        {
            var bucket = _options.PrivateBucket!.Trim();
            var endpoint = BuildBaseEndpoint();
            var requestUri = BuildObjectUri(endpoint, bucket, normalizedKey);
            var now = DateTime.UtcNow;
            var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var payloadHash = ComputeSha256Hex(Array.Empty<byte>());

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
            request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);

            var canonicalHeaders = new StringBuilder()
                .Append("host:").Append(requestUri.Host).Append('\n')
                .Append("x-amz-content-sha256:").Append(payloadHash).Append('\n')
                .Append("x-amz-date:").Append(amzDate).Append('\n')
                .ToString();
            const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
            var canonicalRequest = string.Join(
                "\n",
                "GET",
                requestUri.AbsolutePath,
                string.Empty,
                canonicalHeaders,
                signedHeaders,
                payloadHash);

            var credentialScope = $"{dateStamp}/{GetRegion()}/s3/aws4_request";
            var stringToSign = string.Join(
                "\n",
                "AWS4-HMAC-SHA256",
                amzDate,
                credentialScope,
                ComputeSha256Hex(Encoding.UTF8.GetBytes(canonicalRequest)));
            var signingKey = GetSigningKey(_options.SecretAccessKey!, dateStamp, GetRegion(), "s3");
            var signature = ToHexString(HmacSha256(signingKey, stringToSign));
            var authorization = $"AWS4-HMAC-SHA256 Credential={_options.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Failed to load private document {ObjectKey} from bucket {Bucket}. Status {StatusCode}. Response: {Body}",
                    objectKey,
                    bucket,
                    (int)response.StatusCode,
                    body);
                throw new InvalidOperationException("Stored document could not be retrieved.");
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        var absolutePath = ResolveLocalPath(normalizedKey);
        if (!File.Exists(absolutePath))
        {
            throw new InvalidOperationException("Stored document could not be found.");
        }

        return await File.ReadAllBytesAsync(absolutePath, cancellationToken);
    }

    private bool UseS3()
    {
        if (HasS3Configuration())
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_options.PrivateBucket))
        {
            _logger.LogWarning(
                "Private bucket {Bucket} is configured without complete S3 credentials. Falling back to local private document storage.",
                _options.PrivateBucket);
        }

        return false;
    }

    private bool HasS3Configuration()
    {
        return !string.IsNullOrWhiteSpace(_options.PrivateBucket)
            && !string.IsNullOrWhiteSpace(_options.AccessKeyId)
            && !string.IsNullOrWhiteSpace(_options.SecretAccessKey);
    }

    private async Task UploadToS3Async(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        var bucket = _options.PrivateBucket!.Trim();
        var endpoint = BuildBaseEndpoint();
        var requestUri = BuildObjectUri(endpoint, bucket, objectKey);
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var payloadHash = ComputeSha256Hex(content);

        using var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = new ByteArrayContent(content)
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);

        var canonicalHeaders = new StringBuilder()
            .Append("content-type:").Append(contentType).Append('\n')
            .Append("host:").Append(requestUri.Host).Append('\n')
            .Append("x-amz-content-sha256:").Append(payloadHash).Append('\n')
            .Append("x-amz-date:").Append(amzDate).Append('\n')
            .ToString();
        const string signedHeaders = "content-type;host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest = string.Join(
            "\n",
            "PUT",
            requestUri.AbsolutePath,
            string.Empty,
            canonicalHeaders,
            signedHeaders,
            payloadHash);

        var credentialScope = $"{dateStamp}/{GetRegion()}/s3/aws4_request";
        var stringToSign = string.Join(
            "\n",
            "AWS4-HMAC-SHA256",
            amzDate,
            credentialScope,
            ComputeSha256Hex(Encoding.UTF8.GetBytes(canonicalRequest)));
        var signingKey = GetSigningKey(_options.SecretAccessKey!, dateStamp, GetRegion(), "s3");
        var signature = ToHexString(HmacSha256(signingKey, stringToSign));
        var authorization = $"AWS4-HMAC-SHA256 Credential={_options.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to upload private document {ObjectKey} to bucket {Bucket}. Status {StatusCode}. Response: {Body}",
                objectKey,
                bucket,
                (int)response.StatusCode,
                body);
            throw new InvalidOperationException("Private document upload failed.");
        }
    }

    private Uri BuildObjectUri(string endpoint, string bucket, string objectKey)
    {
        var encodedKey = EncodePath(objectKey);
        if (_options.ForcePathStyle)
        {
            return new Uri($"{endpoint.TrimEnd('/')}/{bucket}/{encodedKey}");
        }

        return endpoint.Contains("amazonaws.com", StringComparison.OrdinalIgnoreCase)
            ? new Uri($"https://{bucket}.s3.{GetRegion()}.amazonaws.com/{encodedKey}")
            : new Uri($"{endpoint.TrimEnd('/')}/{bucket}/{encodedKey}");
    }

    private string BuildBaseEndpoint()
    {
        return string.IsNullOrWhiteSpace(_options.ServiceUrl)
            ? $"https://s3.{GetRegion()}.amazonaws.com"
            : _options.ServiceUrl!.TrimEnd('/');
    }

    private string ResolveLocalPath(string objectKey)
    {
        var normalized = objectKey.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "private-documents", normalized));
    }

    private string GetRegion()
    {
        return string.IsNullOrWhiteSpace(_options.Region) ? "af-south-1" : _options.Region!.Trim();
    }

    private static string NormalizeKey(string objectKey)
    {
        return objectKey.Replace('\\', '/').TrimStart('/');
    }

    private static string EncodePath(string objectKey)
    {
        return string.Join("/", NormalizeKey(objectKey).Split('/').Select(Uri.EscapeDataString));
    }

    private static string ComputeSha256Hex(byte[] bytes)
    {
        return ToHexString(SHA256.HashData(bytes));
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static byte[] GetSigningKey(string secretKey, string dateStamp, string region, string service)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes($"AWS4{secretKey}"), dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        return HmacSha256(kService, "aws4_request");
    }

    private static string ToHexString(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
