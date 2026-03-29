using Advertified.App.Contracts.Packages;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class PackageAreaService : IPackageAreaService
{
    private readonly AppDbContext _db;

    public PackageAreaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PackageAreaOptionResponse>> GetAreasAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            select
                cluster_code as Code,
                display_name as Label,
                coalesce(description, '') as Description
            from package_area_profiles
            where is_active = true
            order by sort_order asc, display_name asc;";

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var results = await connection.QueryAsync<PackageAreaOptionResponse>(
                new CommandDefinition(sql, cancellationToken: cancellationToken));
            return results.ToList();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
