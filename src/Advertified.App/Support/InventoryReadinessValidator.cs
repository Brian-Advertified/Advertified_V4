using Advertified.App.Configuration;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Advertified.App.Support;

internal static class InventoryReadinessValidator
{
    public static async Task ValidateAsync(
        IServiceProvider services,
        string connectionString,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<InventoryReadinessOptions>>().Value;

        if (!options.EnforceOnStartup)
        {
            logger.LogInformation("Inventory readiness check is disabled (InventoryReadiness:EnforceOnStartup=false).");
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var oohInventoryRows = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(*) from ooh_inventory_intelligence where is_active = true;",
            cancellationToken: cancellationToken));

        if (oohInventoryRows < options.MinimumOohInventoryRows)
        {
            logger.LogCritical(
                "Startup blocked: OOH inventory readiness failed. ooh_inventory_intelligence has {RowCount} row(s), minimum required is {MinimumRequired}.",
                oohInventoryRows,
                options.MinimumOohInventoryRows);

            throw new InvalidOperationException(
                $"OOH inventory readiness failed: ooh_inventory_intelligence has {oohInventoryRows} row(s), minimum required is {options.MinimumOohInventoryRows}.");
        }

        logger.LogInformation(
            "Inventory readiness passed. ooh_inventory_intelligence rows: {RowCount} (minimum required: {MinimumRequired}).",
            oohInventoryRows,
            options.MinimumOohInventoryRows);
    }
}
