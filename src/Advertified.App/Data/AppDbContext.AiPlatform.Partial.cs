using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Data;

public partial class AppDbContext
{
    public virtual DbSet<AiPromptTemplate> AiPromptTemplates { get; set; } = null!;
    public virtual DbSet<AiCreativeJobStatus> AiCreativeJobStatuses { get; set; } = null!;
    public virtual DbSet<AiCreativeQaResult> AiCreativeQaResults { get; set; } = null!;
    public virtual DbSet<AiAssetJob> AiAssetJobs { get; set; } = null!;
    public virtual DbSet<AiCreativeJobDeadLetter> AiCreativeJobDeadLetters { get; set; } = null!;
    public virtual DbSet<AiIdempotencyRecord> AiIdempotencyRecords { get; set; } = null!;
    public virtual DbSet<AiUsageLog> AiUsageLogs { get; set; } = null!;
    public virtual DbSet<AiVoiceProfile> AiVoiceProfiles { get; set; } = null!;
    public virtual DbSet<AiVoicePack> AiVoicePacks { get; set; } = null!;
    public virtual DbSet<AiVoicePromptTemplate> AiVoicePromptTemplates { get; set; } = null!;
    public virtual DbSet<AiAdVariant> AiAdVariants { get; set; } = null!;
    public virtual DbSet<AiAdMetric> AiAdMetrics { get; set; } = null!;
}

internal static class AppDbContextAiPlatformModelBuilderExtensions
{
    internal static void ConfigureAiPlatformPersistence(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiPromptTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_prompt_templates_pkey");
            entity.ToTable("ai_prompt_templates");
            entity.HasIndex(e => new { e.Key, e.Channel, e.Language, e.Version }, "uq_ai_prompt_templates_key_channel_language_version").IsUnique();
            entity.HasIndex(e => new { e.Key, e.Channel, e.Language, e.IsActive }, "ix_ai_prompt_templates_key_channel_language_active");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Key)
                .HasMaxLength(120)
                .HasColumnName("key");
            entity.Property(e => e.Channel)
                .HasMaxLength(40)
                .HasColumnName("channel");
            entity.Property(e => e.Language)
                .HasMaxLength(40)
                .HasColumnName("language");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.SystemPrompt).HasColumnName("system_prompt");
            entity.Property(e => e.TemplatePrompt).HasColumnName("template_prompt");
            entity.Property(e => e.OutputSchema).HasColumnName("output_schema");
            entity.Property(e => e.VariablesJson)
                .HasColumnName("variables_json");
            entity.Property(e => e.VersionLabel)
                .HasMaxLength(30)
                .HasDefaultValue("v1")
                .HasColumnName("version_label");
            entity.Property(e => e.PerformanceScore)
                .HasPrecision(5, 2)
                .HasColumnName("performance_score");
            entity.Property(e => e.UsageCount)
                .HasDefaultValue(0)
                .HasColumnName("usage_count");
            entity.Property(e => e.BaseSystemPromptKey)
                .HasMaxLength(120)
                .HasColumnName("base_system_prompt_key");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
        });

        modelBuilder.Entity<AiCreativeJobStatus>(entity =>
        {
            entity.HasKey(e => e.JobId).HasName("ai_creative_job_statuses_pkey");
            entity.ToTable("ai_creative_job_statuses");
            entity.HasIndex(e => e.CampaignId, "ix_ai_creative_job_statuses_campaign_id");
            entity.HasIndex(e => e.UpdatedAt, "ix_ai_creative_job_statuses_updated_at");

            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .HasColumnName("status");
            entity.Property(e => e.Error).HasColumnName("error");
            entity.Property(e => e.RetryAttemptCount)
                .HasDefaultValue(0)
                .HasColumnName("retry_attempt_count");
            entity.Property(e => e.LastFailure).HasColumnName("last_failure");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<AiCreativeQaResult>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_creative_qa_results_pkey");
            entity.ToTable("ai_creative_qa_results");
            entity.HasIndex(e => e.CampaignId, "ix_ai_creative_qa_results_campaign_id");
            entity.HasIndex(e => e.CreativeId, "ix_ai_creative_qa_results_creative_id");
            entity.HasIndex(e => e.CreatedAt, "ix_ai_creative_qa_results_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreativeId).HasColumnName("creative_id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.Channel)
                .HasMaxLength(40)
                .HasColumnName("channel");
            entity.Property(e => e.Language)
                .HasMaxLength(40)
                .HasColumnName("language");
            entity.Property(e => e.Clarity).HasPrecision(5, 2).HasColumnName("clarity");
            entity.Property(e => e.Attention).HasPrecision(5, 2).HasColumnName("attention");
            entity.Property(e => e.EmotionalImpact).HasPrecision(5, 2).HasColumnName("emotional_impact");
            entity.Property(e => e.CtaStrength).HasPrecision(5, 2).HasColumnName("cta_strength");
            entity.Property(e => e.BrandFit).HasPrecision(5, 2).HasColumnName("brand_fit");
            entity.Property(e => e.ChannelFit).HasPrecision(5, 2).HasColumnName("channel_fit");
            entity.Property(e => e.FinalScore).HasPrecision(5, 2).HasColumnName("final_score");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .HasColumnName("status");
            entity.Property(e => e.RiskLevel)
                .HasMaxLength(20)
                .HasColumnName("risk_level");
            entity.Property(e => e.IssuesJson).HasColumnName("issues_json");
            entity.Property(e => e.SuggestionsJson).HasColumnName("suggestions_json");
            entity.Property(e => e.RiskFlagsJson).HasColumnName("risk_flags_json");
            entity.Property(e => e.ImprovedPayloadJson).HasColumnName("improved_payload_json");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
        });

        modelBuilder.Entity<AiAssetJob>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_asset_jobs_pkey");
            entity.ToTable("ai_asset_jobs");
            entity.HasIndex(e => e.CampaignId, "ix_ai_asset_jobs_campaign_id");
            entity.HasIndex(e => e.CreativeId, "ix_ai_asset_jobs_creative_id");
            entity.HasIndex(e => e.Status, "ix_ai_asset_jobs_status");
            entity.HasIndex(e => e.CreatedAt, "ix_ai_asset_jobs_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.CreativeId).HasColumnName("creative_id");
            entity.Property(e => e.AssetKind)
                .HasMaxLength(40)
                .HasColumnName("asset_kind");
            entity.Property(e => e.Provider)
                .HasMaxLength(80)
                .HasColumnName("provider");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasColumnName("status");
            entity.Property(e => e.RequestJson).HasColumnName("request_json");
            entity.Property(e => e.ResultJson).HasColumnName("result_json");
            entity.Property(e => e.AssetUrl).HasColumnName("asset_url");
            entity.Property(e => e.AssetType)
                .HasMaxLength(30)
                .HasColumnName("asset_type");
            entity.Property(e => e.Error).HasColumnName("error");
            entity.Property(e => e.RetryAttemptCount)
                .HasDefaultValue(0)
                .HasColumnName("retry_attempt_count");
            entity.Property(e => e.LastFailure).HasColumnName("last_failure");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
        });

        modelBuilder.Entity<AiCreativeJobDeadLetter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_creative_job_dead_letters_pkey");
            entity.ToTable("ai_creative_job_dead_letters");
            entity.HasIndex(e => e.JobId, "ix_ai_creative_job_dead_letters_job_id");
            entity.HasIndex(e => e.CreatedAt, "ix_ai_creative_job_dead_letters_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
        });

        modelBuilder.Entity<AiIdempotencyRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_idempotency_records_pkey");
            entity.ToTable("ai_idempotency_records");
            entity.HasIndex(e => new { e.Scope, e.Key }, "uq_ai_idempotency_records_scope_key").IsUnique();
            entity.HasIndex(e => e.CreatedAt, "ix_ai_idempotency_records_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Scope)
                .HasMaxLength(80)
                .HasColumnName("scope");
            entity.Property(e => e.Key)
                .HasMaxLength(256)
                .HasColumnName("key");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
        });

        modelBuilder.Entity<AiUsageLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_usage_logs_pkey");
            entity.ToTable("ai_usage_logs");
            entity.HasIndex(e => e.CampaignId, "ix_ai_usage_logs_campaign_id");
            entity.HasIndex(e => e.Status, "ix_ai_usage_logs_status");
            entity.HasIndex(e => e.CreatedAt, "ix_ai_usage_logs_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.CreativeId).HasColumnName("creative_id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.Operation)
                .HasMaxLength(80)
                .HasColumnName("operation");
            entity.Property(e => e.Provider)
                .HasMaxLength(80)
                .HasColumnName("provider");
            entity.Property(e => e.EstimatedCostZar)
                .HasPrecision(12, 2)
                .HasColumnName("estimated_cost_zar");
            entity.Property(e => e.ActualCostZar)
                .HasPrecision(12, 2)
                .HasColumnName("actual_cost_zar");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasColumnName("status");
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<AiVoiceProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_voice_profiles_pkey");
            entity.ToTable("ai_voice_profiles");
            entity.HasIndex(e => new { e.Provider, e.Label }, "uq_ai_voice_profiles_provider_label").IsUnique();
            entity.HasIndex(e => new { e.Provider, e.IsActive, e.SortOrder }, "ix_ai_voice_profiles_provider_active_sort");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Provider)
                .HasMaxLength(40)
                .HasColumnName("provider");
            entity.Property(e => e.Label)
                .HasMaxLength(120)
                .HasColumnName("label");
            entity.Property(e => e.VoiceId)
                .HasMaxLength(120)
                .HasColumnName("voice_id");
            entity.Property(e => e.Language)
                .HasMaxLength(40)
                .HasColumnName("language");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0)
                .HasColumnName("sort_order");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<AiVoicePack>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_voice_packs_pkey");
            entity.ToTable("ai_voice_packs");
            entity.HasIndex(e => new { e.Provider, e.Name }, "uq_ai_voice_packs_provider_name").IsUnique();
            entity.HasIndex(e => new { e.Provider, e.IsActive, e.SortOrder }, "ix_ai_voice_packs_provider_active_sort");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Provider)
                .HasMaxLength(40)
                .HasColumnName("provider");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.Accent)
                .HasMaxLength(80)
                .HasColumnName("accent");
            entity.Property(e => e.Language)
                .HasMaxLength(40)
                .HasColumnName("language");
            entity.Property(e => e.Tone)
                .HasMaxLength(80)
                .HasColumnName("tone");
            entity.Property(e => e.Persona)
                .HasMaxLength(120)
                .HasColumnName("persona");
            entity.Property(e => e.UseCasesJson)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnName("use_cases_json");
            entity.Property(e => e.VoiceId)
                .HasMaxLength(120)
                .HasColumnName("voice_id");
            entity.Property(e => e.SampleAudioUrl).HasColumnName("sample_audio_url");
            entity.Property(e => e.PromptTemplate).HasColumnName("prompt_template");
            entity.Property(e => e.PricingTier)
                .HasMaxLength(20)
                .HasDefaultValue("standard")
                .HasColumnName("pricing_tier");
            entity.Property(e => e.IsClientSpecific)
                .HasDefaultValue(false)
                .HasColumnName("is_client_specific");
            entity.Property(e => e.ClientUserId)
                .HasColumnName("client_user_id");
            entity.Property(e => e.IsClonedVoice)
                .HasDefaultValue(false)
                .HasColumnName("is_cloned_voice");
            entity.Property(e => e.AudienceTagsJson)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnName("audience_tags_json");
            entity.Property(e => e.ObjectiveTagsJson)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnName("objective_tags_json");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0)
                .HasColumnName("sort_order");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<AiVoicePromptTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_voice_prompt_templates_pkey");
            entity.ToTable("ai_voice_prompt_templates");
            entity.HasIndex(e => e.TemplateNumber, "uq_ai_voice_prompt_templates_template_number").IsUnique();
            entity.HasIndex(e => e.Category, "ix_ai_voice_prompt_templates_category");
            entity.HasIndex(e => new { e.IsActive, e.SortOrder, e.TemplateNumber }, "ix_ai_voice_prompt_templates_active_sort");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.TemplateNumber).HasColumnName("template_number");
            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.PromptTemplate).HasColumnName("prompt_template");
            entity.Property(e => e.PrimaryVoicePackName).HasColumnName("primary_voice_pack_name");
            entity.Property(e => e.FallbackVoicePackNamesJson)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnName("fallback_voice_pack_names_json");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0)
                .HasColumnName("sort_order");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<AiAdVariant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_ad_variants_pkey");
            entity.ToTable("ai_ad_variants");
            entity.HasIndex(e => e.CampaignId, "ix_ai_ad_variants_campaign_id");
            entity.HasIndex(e => e.Platform, "ix_ai_ad_variants_platform");
            entity.HasIndex(e => e.Status, "ix_ai_ad_variants_status");
            entity.HasIndex(e => e.PublishedAt, "ix_ai_ad_variants_published_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.CampaignCreativeId).HasColumnName("campaign_creative_id");
            entity.Property(e => e.Platform)
                .HasMaxLength(40)
                .HasColumnName("platform");
            entity.Property(e => e.Channel)
                .HasMaxLength(40)
                .HasColumnName("channel");
            entity.Property(e => e.Language)
                .HasMaxLength(40)
                .HasColumnName("language");
            entity.Property(e => e.TemplateId).HasColumnName("template_id");
            entity.Property(e => e.VoicePackId).HasColumnName("voice_pack_id");
            entity.Property(e => e.VoicePackName)
                .HasMaxLength(150)
                .HasColumnName("voice_pack_name");
            entity.Property(e => e.Script).HasColumnName("script");
            entity.Property(e => e.AudioAssetUrl).HasColumnName("audio_asset_url");
            entity.Property(e => e.PlatformAdId)
                .HasMaxLength(160)
                .HasColumnName("platform_ad_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasColumnName("status");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
        });

        modelBuilder.Entity<AiAdMetric>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_ad_metrics_pkey");
            entity.ToTable("ai_ad_metrics");
            entity.HasIndex(e => e.CampaignId, "ix_ai_ad_metrics_campaign_id");
            entity.HasIndex(e => e.AdVariantId, "ix_ai_ad_metrics_ad_variant_id");
            entity.HasIndex(e => e.RecordedAt, "ix_ai_ad_metrics_recorded_at");
            entity.HasIndex(e => e.Platform, "ix_ai_ad_metrics_platform");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.AdVariantId).HasColumnName("ad_variant_id");
            entity.Property(e => e.Platform)
                .HasMaxLength(40)
                .HasColumnName("platform");
            entity.Property(e => e.Source)
                .HasMaxLength(30)
                .HasColumnName("source");
            entity.Property(e => e.Impressions).HasColumnName("impressions");
            entity.Property(e => e.Clicks).HasColumnName("clicks");
            entity.Property(e => e.Conversions).HasColumnName("conversions");
            entity.Property(e => e.CostZar)
                .HasPrecision(12, 2)
                .HasColumnName("cost_zar");
            entity.Property(e => e.Ctr)
                .HasPrecision(8, 4)
                .HasColumnName("ctr");
            entity.Property(e => e.ConversionRate)
                .HasPrecision(8, 4)
                .HasColumnName("conversion_rate");
            entity.Property(e => e.RecordedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("recorded_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
        });
    }
}
