namespace Advertified.App.Services.Abstractions;

public interface IBroadcastLanguagePriorityService
{
    Task<IReadOnlyList<string>> OrderRequestedLanguagesAsync(IEnumerable<string> languages, CancellationToken cancellationToken);
}

