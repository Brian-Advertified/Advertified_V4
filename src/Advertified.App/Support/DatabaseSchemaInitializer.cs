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
                     Path.Combine("database", "media_import", "001_create_tables.sql"),
                     Path.Combine("database", "bootstrap", "002_normalized_media_catalog.sql"),
                     Path.Combine("database", "bootstrap", "003_payment_audit.sql"),
                     Path.Combine("database", "bootstrap", "004_invoicing.sql"),
                     Path.Combine("database", "bootstrap", "005_agent_inbox.sql"),
                     Path.Combine("database", "bootstrap", "006_campaign_email_lifecycle.sql"),
                     Path.Combine("database", "bootstrap", "007_package_area_profiles.sql"),
                     Path.Combine("database", "bootstrap", "008_broadcast_inventory_v2.sql"),
                     Path.Combine("database", "bootstrap", "009_remove_legacy_broadcast_v1.sql"),
                     Path.Combine("database", "bootstrap", "010_recommendation_revisions.sql"),
                      Path.Combine("database", "bootstrap", "011_recommendation_pdf_storage.sql"),
                      Path.Combine("database", "bootstrap", "012_admin_engine_policy_overrides.sql"),
                       Path.Combine("database", "bootstrap", "013_change_audit_log.sql"),
                       Path.Combine("database", "bootstrap", "014_agent_area_assignments.sql"),
                     Path.Combine("database", "bootstrap", "015_creative_director_role.sql"),
                     Path.Combine("database", "bootstrap", "016_campaign_messages.sql"),
                     Path.Combine("database", "bootstrap", "017_consent_preferences.sql"),
                     Path.Combine("database", "bootstrap", "018_campaign_operations_controls.sql"),
                     Path.Combine("database", "bootstrap", "019_pricing_settings.sql"),
                     Path.Combine("database", "bootstrap", "020_campaign_execution_and_storage.sql"),
                     Path.Combine("database", "bootstrap", "021_campaign_creative_systems.sql"),
                     Path.Combine("database", "bootstrap", "022_invoice_supporting_documents.sql"),
                     Path.Combine("database", "bootstrap", "023_notification_read_receipts.sql"),
                     Path.Combine("database", "bootstrap", "024_seed_ooh_baseline.sql"),
                     Path.Combine("database", "bootstrap", "025_creative_generation_pipeline.sql"),
                     Path.Combine("database", "bootstrap", "026_ai_platform_production_hardening.sql"),
                     Path.Combine("database", "bootstrap", "027_ai_voice_profiles.sql"),
                     Path.Combine("database", "bootstrap", "028_campaign_brief_video_preferences.sql"),
                     Path.Combine("database", "bootstrap", "029_ai_voice_packs.sql"),
                     Path.Combine("database", "bootstrap", "030_ai_voice_pack_phase2_phase3.sql"),
                     Path.Combine("database", "bootstrap", "031_ai_voice_prompt_library.sql"),
                     Path.Combine("database", "bootstrap", "032_ai_ad_operations.sql"),
                     Path.Combine("database", "bootstrap", "033_package_band_ai_entitlements.sql"),
                     Path.Combine("database", "bootstrap", "034_campaign_brief_strategy_intelligence.sql"),
                     Path.Combine("database", "bootstrap", "035_form_option_items.sql"),
                     Path.Combine("database", "bootstrap", "036_inventory_strategy_intelligence.sql"),
                     Path.Combine("database", "bootstrap", "037_prospect_leads.sql"),
                      Path.Combine("database", "bootstrap", "038_social_inventory_seed.sql"),
                      Path.Combine("database", "bootstrap", "039_leads_and_signals.sql"),
                      Path.Combine("database", "bootstrap", "040_lead_insight_history.sql"),
                      Path.Combine("database", "bootstrap", "041_lead_source_ingestion.sql"),
                      Path.Combine("database", "bootstrap", "042_lead_actions.sql"),
                      Path.Combine("database", "bootstrap", "043_lead_interactions.sql"),
                      Path.Combine("database", "bootstrap", "044_lead_action_assignment.sql")
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
