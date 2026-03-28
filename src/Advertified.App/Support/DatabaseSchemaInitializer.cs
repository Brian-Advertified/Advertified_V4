using Advertified.App.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Advertified.App.Support;

internal static class DatabaseSchemaInitializer
{
    internal static async Task InitializeAsync(IServiceProvider services, IWebHostEnvironment environment)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var baseDirectories = new[]
        {
            Directory.GetCurrentDirectory(),
            environment.ContentRootPath,
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."))
        }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        foreach (var relativePath in new[]
                 {
                     Path.Combine("database", "bootstrap", "001_package_catalog.sql"),
                     Path.Combine("database", "bootstrap", "002_normalized_media_catalog.sql"),
                     Path.Combine("database", "bootstrap", "003_payment_audit.sql"),
                     Path.Combine("database", "bootstrap", "004_invoicing.sql"),
                     Path.Combine("database", "bootstrap", "005_agent_inbox.sql")
                 })
        {
            var fullPath = baseDirectories
                .Select(baseDirectory => Path.Combine(baseDirectory, relativePath))
                .FirstOrDefault(File.Exists);

            if (string.IsNullOrWhiteSpace(fullPath))
            {
                continue;
            }

            var script = await File.ReadAllTextAsync(fullPath);
            if (!string.IsNullOrWhiteSpace(script))
            {
                var connection = db.Database.GetDbConnection();
                var shouldClose = connection.State != ConnectionState.Open;
                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                await using var command = connection.CreateCommand();
                command.CommandText = script;
                await command.ExecuteNonQueryAsync();

                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}
