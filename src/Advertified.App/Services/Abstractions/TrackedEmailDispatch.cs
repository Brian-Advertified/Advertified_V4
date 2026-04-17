namespace Advertified.App.Services.Abstractions;

public sealed class TrackedEmailDispatch
{
    public Guid DispatchId { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
}
