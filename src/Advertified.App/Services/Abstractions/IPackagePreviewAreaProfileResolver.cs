using Npgsql;

namespace Advertified.App.Services.Abstractions;

public interface IPackagePreviewAreaProfileResolver
{
    Task<PackagePreviewAreaProfile> ResolveAsync(NpgsqlConnection connection, string? selectedArea, CancellationToken cancellationToken);
}
