namespace Advertified.App.Services.Abstractions;

public interface ICurrentUserAccessor
{
    Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken);
}
