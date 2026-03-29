using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Support;

internal static class EmailTemplateInitializer
{
    internal static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("EmailTemplateInitializer");

        await EnsureTableAsync(db, cancellationToken);
        await SeedDefaultsAsync(db, logger, cancellationToken);
    }

    private static async Task EnsureTableAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        const string sql = @"
            create table if not exists email_templates (
                id uuid primary key default gen_random_uuid(),
                template_name varchar(120) not null,
                subject_template text not null,
                body_html_template text not null,
                is_active boolean not null default true,
                created_at_utc timestamp without time zone not null default timezone('utc', now()),
                updated_at_utc timestamp without time zone not null default timezone('utc', now())
            );

            create unique index if not exists uq_email_templates_template_name
                on email_templates (template_name);

            create index if not exists ix_email_templates_is_active
                on email_templates (is_active);
            ";

        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task SeedDefaultsAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var existingTemplates = await db.EmailTemplates
            .ToListAsync(cancellationToken);
        var existingByName = existingTemplates.ToDictionary(x => x.TemplateName, StringComparer.OrdinalIgnoreCase);
        var inserted = 0;
        var updated = 0;

        foreach (var template in EmailTemplateDefaults.CreateDefaults())
        {
            if (existingByName.TryGetValue(template.TemplateName, out var existing))
            {
                existing.SubjectTemplate = template.SubjectTemplate;
                existing.BodyHtmlTemplate = template.BodyHtmlTemplate;
                existing.IsActive = template.IsActive;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                updated++;
            }
            else
            {
                db.EmailTemplates.Add(template);
                inserted++;
            }
        }

        if (inserted == 0 && updated == 0)
        {
            logger.LogInformation("Email template defaults already present.");
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Synchronized email templates. Inserted {InsertedCount}, updated {UpdatedCount}.", inserted, updated);
    }
}
