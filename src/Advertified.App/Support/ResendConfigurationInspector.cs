using Advertified.App.Configuration;

namespace Advertified.App.Support;

internal static class ResendConfigurationInspector
{
    public static bool HasApiKey(ResendOptions options)
        => !string.IsNullOrWhiteSpace(options.ApiKey);

    public static bool HasAnySender(ResendOptions options)
        => !string.IsNullOrWhiteSpace(options.FromEmail)
           || options.SenderAddresses.Values.Any(value => !string.IsNullOrWhiteSpace(value));

    public static bool HasValidBaseUrl(ResendOptions options)
        => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static bool IsSendConfigured(ResendOptions options)
        => HasApiKey(options) && HasAnySender(options);
}
