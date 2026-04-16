using Advertified.App.Contracts.Public;

namespace Advertified.App.Services.Abstractions;

public interface IPublicLocationSearchService
{
    Task<IReadOnlyList<string>> ListSuburbsAsync(string city, CancellationToken cancellationToken);

    Task<IReadOnlyList<PublicLocationSuggestionResponse>> SearchAsync(
        string query,
        string? geographyScope,
        string? city,
        int maxResults,
        CancellationToken cancellationToken);
}
