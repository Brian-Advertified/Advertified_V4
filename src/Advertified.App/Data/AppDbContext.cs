using System;
using System.Collections.Generic;
using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BusinessProfile> BusinessProfiles { get; set; }

    public virtual DbSet<AgentAreaAssignment> AgentAreaAssignments { get; set; }

    public virtual DbSet<Campaign> Campaigns { get; set; }

    public virtual DbSet<CampaignBrief> CampaignBriefs { get; set; }

    public virtual DbSet<CampaignBriefDraft> CampaignBriefDrafts { get; set; }

    public virtual DbSet<CampaignAsset> CampaignAssets { get; set; }

    public virtual DbSet<CampaignCreativeSystem> CampaignCreativeSystems { get; set; }

    public virtual DbSet<CampaignCreative> CampaignCreatives { get; set; }

    public virtual DbSet<CreativeScore> CreativeScores { get; set; }

    public virtual DbSet<CampaignConversation> CampaignConversations { get; set; }

    public virtual DbSet<CampaignDeliveryReport> CampaignDeliveryReports { get; set; }

    public virtual DbSet<CampaignMessage> CampaignMessages { get; set; }

    public virtual DbSet<CampaignPauseWindow> CampaignPauseWindows { get; set; }

    public virtual DbSet<CampaignRecommendation> CampaignRecommendations { get; set; }

    public virtual DbSet<CampaignSupplierBooking> CampaignSupplierBookings { get; set; }

    public virtual DbSet<ChangeAuditLog> ChangeAuditLogs { get; set; }

    public virtual DbSet<ConsentPreference> ConsentPreferences { get; set; }

    public virtual DbSet<FormOptionItem> FormOptionItems { get; set; }

    public virtual DbSet<EmailTemplate> EmailTemplates { get; set; }

    public virtual DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }

    public virtual DbSet<IdentityProfile> IdentityProfiles { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceIssuerProfile> InvoiceIssuerProfiles { get; set; }

    public virtual DbSet<InvoiceLineItem> InvoiceLineItems { get; set; }

    public virtual DbSet<LegalDocument> LegalDocuments { get; set; }

    public virtual DbSet<NotificationReadReceipt> NotificationReadReceipts { get; set; }

    public virtual DbSet<PackageBand> PackageBands { get; set; }

    public virtual DbSet<PackageOrder> PackageOrders { get; set; }

    public virtual DbSet<PricingSetting> PricingSettings { get; set; }

    public virtual DbSet<PaymentProviderRequestAudit> PaymentProviderRequests { get; set; }

    public virtual DbSet<PaymentProviderWebhookAudit> PaymentProviderWebhooks { get; set; }

    public virtual DbSet<RecommendationItem> RecommendationItems { get; set; }

    public virtual DbSet<UserAccount> UserAccounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("account_status", new[] { "pending_verification", "active", "suspended" })
            .HasPostgresEnum("identity_type", new[] { "sa_id", "passport" })
            .HasPostgresEnum("user_role", new[] { "client", "agent", "creative_director", "admin" })
            .HasPostgresEnum("verification_status", new[] { "not_submitted", "submitted", "verified", "failed", "rejected" })
            .HasPostgresExtension("pgcrypto");

        modelBuilder.Entity<BusinessProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("business_profiles_pkey");

            entity.ToTable("business_profiles");

            entity.HasIndex(e => e.RegistrationNumber, "ix_business_profiles_registration_number");

            entity.HasIndex(e => e.RegistrationNumber, "uq_business_profiles_registration_number").IsUnique();

            entity.HasIndex(e => e.UserId, "uq_business_profiles_user_id").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AnnualRevenueBand)
                .HasMaxLength(50)
                .HasColumnName("annual_revenue_band");
            entity.Property(e => e.BusinessName)
                .HasMaxLength(200)
                .HasColumnName("business_name");
            entity.Property(e => e.BusinessType)
                .HasMaxLength(100)
                .HasColumnName("business_type");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Industry)
                .HasMaxLength(100)
                .HasColumnName("industry");
            entity.Property(e => e.Province)
                .HasMaxLength(100)
                .HasColumnName("province");
            entity.Property(e => e.RegistrationNumber)
                .HasMaxLength(30)
                .HasColumnName("registration_number");
            entity.Property(e => e.StreetAddress)
                .HasMaxLength(255)
                .HasColumnName("street_address");
            entity.Property(e => e.TradingAsName)
                .HasMaxLength(200)
                .HasColumnName("trading_as_name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VatNumber)
                .HasMaxLength(30)
                .HasColumnName("vat_number");

            entity.HasOne(d => d.User).WithOne(p => p.BusinessProfile)
                .HasForeignKey<BusinessProfile>(d => d.UserId)
                .HasConstraintName("business_profiles_user_id_fkey");
        });

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaigns_pkey");

            entity.ToTable("campaigns");

            entity.HasIndex(e => e.Status, "ix_campaigns_status");

            entity.HasIndex(e => e.UserId, "ix_campaigns_user_id");

            entity.HasIndex(e => e.PackageOrderId, "uq_campaign_package_order").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AgentAssistanceRequested).HasColumnName("agent_assistance_requested");
            entity.Property(e => e.AiUnlocked).HasColumnName("ai_unlocked");
            entity.Property(e => e.CampaignName)
                .HasMaxLength(200)
                .HasColumnName("campaign_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.PackageBandId).HasColumnName("package_band_id");
            entity.Property(e => e.PackageOrderId).HasColumnName("package_order_id");
            entity.Property(e => e.PausedAt).HasColumnName("paused_at");
            entity.Property(e => e.PauseReason).HasColumnName("pause_reason");
            entity.Property(e => e.PlanningMode)
                .HasMaxLength(50)
                .HasColumnName("planning_mode");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'paid'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TotalPausedDays)
                .HasDefaultValue(0)
                .HasColumnName("total_paused_days");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.PackageBand).WithMany(p => p.Campaigns)
                .HasForeignKey(d => d.PackageBandId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("campaigns_package_band_id_fkey");

            entity.HasOne(d => d.PackageOrder).WithOne(p => p.Campaign)
                .HasForeignKey<Campaign>(d => d.PackageOrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("campaigns_package_order_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Campaigns)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("campaigns_user_id_fkey");
        });

        modelBuilder.Entity<CampaignBrief>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_briefs_pkey");

            entity.ToTable("campaign_briefs");

            entity.HasIndex(e => e.CampaignId, "ix_campaign_briefs_campaign_id");

            entity.HasIndex(e => e.CampaignId, "uq_campaign_briefs_campaign_id").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AdditionalBudget)
                .HasPrecision(12, 2)
                .HasColumnName("additional_budget");
            entity.Property(e => e.AudienceClarity)
                .HasMaxLength(50)
                .HasColumnName("audience_clarity");
            entity.Property(e => e.AverageCustomerSpendBand)
                .HasMaxLength(50)
                .HasColumnName("average_customer_spend_band");
            entity.Property(e => e.AreasJson)
                .HasColumnType("jsonb")
                .HasColumnName("areas_json");
            entity.Property(e => e.BusinessStage)
                .HasMaxLength(50)
                .HasColumnName("business_stage");
            entity.Property(e => e.BuyingBehaviour)
                .HasMaxLength(50)
                .HasColumnName("buying_behaviour");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.CitiesJson)
                .HasColumnType("jsonb")
                .HasColumnName("cities_json");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreativeNotes).HasColumnName("creative_notes");
            entity.Property(e => e.CreativeReady).HasColumnName("creative_ready");
            entity.Property(e => e.CustomerType)
                .HasMaxLength(50)
                .HasColumnName("customer_type");
            entity.Property(e => e.DecisionCycle)
                .HasMaxLength(50)
                .HasColumnName("decision_cycle");
            entity.Property(e => e.DurationWeeks).HasColumnName("duration_weeks");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.ExcludedAreasJson)
                .HasColumnType("jsonb")
                .HasColumnName("excluded_areas_json");
            entity.Property(e => e.ExcludedMediaTypesJson)
                .HasColumnType("jsonb")
                .HasColumnName("excluded_media_types_json");
            entity.Property(e => e.GeographyScope)
                .HasMaxLength(50)
                .HasColumnName("geography_scope");
            entity.Property(e => e.GrowthTarget)
                .HasMaxLength(50)
                .HasColumnName("growth_target");
            entity.Property(e => e.MaxMediaItems).HasColumnName("max_media_items");
            entity.Property(e => e.MonthlyRevenueBand)
                .HasMaxLength(50)
                .HasColumnName("monthly_revenue_band");
            entity.Property(e => e.MustHaveAreasJson)
                .HasColumnType("jsonb")
                .HasColumnName("must_have_areas_json");
            entity.Property(e => e.Objective)
                .HasMaxLength(100)
                .HasColumnName("objective");
            entity.Property(e => e.OpenToUpsell).HasColumnName("open_to_upsell");
            entity.Property(e => e.PricePositioning)
                .HasMaxLength(50)
                .HasColumnName("price_positioning");
            entity.Property(e => e.PreferredVideoAspectRatio)
                .HasMaxLength(10)
                .HasColumnName("preferred_video_aspect_ratio");
            entity.Property(e => e.PreferredVideoDurationSeconds).HasColumnName("preferred_video_duration_seconds");
            entity.Property(e => e.PreferredMediaTypesJson)
                .HasColumnType("jsonb")
                .HasColumnName("preferred_media_types_json");
            entity.Property(e => e.ProvincesJson)
                .HasColumnType("jsonb")
                .HasColumnName("provinces_json");
            entity.Property(e => e.SalesModel)
                .HasMaxLength(50)
                .HasColumnName("sales_model");
            entity.Property(e => e.SpecialRequirements).HasColumnName("special_requirements");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.SuburbsJson)
                .HasColumnType("jsonb")
                .HasColumnName("suburbs_json");
            entity.Property(e => e.TargetAgeMax).HasColumnName("target_age_max");
            entity.Property(e => e.TargetAgeMin).HasColumnName("target_age_min");
            entity.Property(e => e.TargetAudienceNotes).HasColumnName("target_audience_notes");
            entity.Property(e => e.TargetGender)
                .HasMaxLength(30)
                .HasColumnName("target_gender");
            entity.Property(e => e.TargetInterestsJson)
                .HasColumnType("jsonb")
                .HasColumnName("target_interests_json");
            entity.Property(e => e.TargetLanguagesJson)
                .HasColumnType("jsonb")
                .HasColumnName("target_languages_json");
            entity.Property(e => e.TargetLsmMax).HasColumnName("target_lsm_max");
            entity.Property(e => e.TargetLsmMin).HasColumnName("target_lsm_min");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UrgencyLevel)
                .HasMaxLength(50)
                .HasColumnName("urgency_level");
            entity.Property(e => e.ValuePropositionFocus)
                .HasMaxLength(50)
                .HasColumnName("value_proposition_focus");

            entity.HasOne(d => d.Campaign).WithOne(p => p.CampaignBrief)
                .HasForeignKey<CampaignBrief>(d => d.CampaignId)
                .HasConstraintName("campaign_briefs_campaign_id_fkey");
        });

        modelBuilder.Entity<CampaignBriefDraft>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_brief_drafts_pkey");

            entity.ToTable("campaign_brief_drafts");

            entity.HasIndex(e => e.CampaignId, "uq_campaign_brief_drafts_campaign_id").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.DraftJson)
                .HasColumnType("jsonb")
                .HasColumnName("draft_json");
            entity.Property(e => e.SavedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("saved_at");

            entity.HasOne(d => d.Campaign).WithOne(p => p.CampaignBriefDraft)
                .HasForeignKey<CampaignBriefDraft>(d => d.CampaignId)
                .HasConstraintName("campaign_brief_drafts_campaign_id_fkey");
        });

        modelBuilder.Entity<CampaignAsset>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_assets_pkey");

            entity.ToTable("campaign_assets");

            entity.HasIndex(e => e.CampaignId, "ix_campaign_assets_campaign_id");
            entity.HasIndex(e => e.AssetType, "ix_campaign_assets_asset_type");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AssetType)
                .HasMaxLength(50)
                .HasColumnName("asset_type");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.ContentType)
                .HasMaxLength(255)
                .HasColumnName("content_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(255)
                .HasColumnName("display_name");
            entity.Property(e => e.PublicUrl).HasColumnName("public_url");
            entity.Property(e => e.SizeBytes).HasColumnName("size_bytes");
            entity.Property(e => e.StorageObjectKey).HasColumnName("storage_object_key");
            entity.Property(e => e.UploadedByUserId).HasColumnName("uploaded_by_user_id");

            entity.HasOne(d => d.Campaign).WithMany(p => p.CampaignAssets)
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_assets_campaign_id_fkey");

            entity.HasOne(d => d.UploadedByUser).WithMany()
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaign_assets_uploaded_by_user_id_fkey");
        });

        modelBuilder.Entity<CampaignCreativeSystem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_creative_systems_pkey");

            entity.ToTable("campaign_creative_systems");

            entity.HasIndex(e => e.CampaignId, "ix_campaign_creative_systems_campaign_id");
            entity.HasIndex(e => e.CreatedAt, "ix_campaign_creative_systems_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.Prompt).HasColumnName("prompt");
            entity.Property(e => e.IterationLabel)
                .HasMaxLength(100)
                .HasColumnName("iteration_label");
            entity.Property(e => e.InputJson)
                .HasColumnType("jsonb")
                .HasColumnName("input_json");
            entity.Property(e => e.OutputJson)
                .HasColumnType("jsonb")
                .HasColumnName("output_json");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Campaign).WithMany(p => p.CampaignCreativeSystems)
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_creative_systems_campaign_id_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaign_creative_systems_created_by_user_id_fkey");
        });

        modelBuilder.Entity<CampaignCreative>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_creatives_pkey");

            entity.ToTable("campaign_creatives");

            entity.HasIndex(e => e.CampaignId, "ix_campaign_creatives_campaign_id");
            entity.HasIndex(e => e.Channel, "ix_campaign_creatives_channel");
            entity.HasIndex(e => e.CreatedAt, "ix_campaign_creatives_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.SourceCreativeSystemId).HasColumnName("source_creative_system_id");
            entity.Property(e => e.Channel)
                .HasMaxLength(40)
                .HasColumnName("channel");
            entity.Property(e => e.Language)
                .HasMaxLength(40)
                .HasDefaultValue("English")
                .HasColumnName("language");
            entity.Property(e => e.CreativeType)
                .HasMaxLength(80)
                .HasColumnName("creative_type");
            entity.Property(e => e.JsonPayload)
                .HasColumnType("jsonb")
                .HasColumnName("json_payload");
            entity.Property(e => e.Score)
                .HasPrecision(5, 2)
                .HasColumnName("score");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Campaign).WithMany(p => p.CampaignCreatives)
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_creatives_campaign_id_fkey");

            entity.HasOne(d => d.SourceCreativeSystem).WithMany(p => p.CampaignCreatives)
                .HasForeignKey(d => d.SourceCreativeSystemId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaign_creatives_source_creative_system_id_fkey");
        });

        modelBuilder.Entity<CreativeScore>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("creative_scores_pkey");

            entity.ToTable("creative_scores");

            entity.HasIndex(e => e.CampaignCreativeId, "ix_creative_scores_campaign_creative_id");
            entity.HasIndex(e => new { e.CampaignCreativeId, e.MetricName }, "uq_creative_scores_creative_metric")
                .IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignCreativeId).HasColumnName("campaign_creative_id");
            entity.Property(e => e.MetricName)
                .HasMaxLength(80)
                .HasColumnName("metric_name");
            entity.Property(e => e.MetricValue)
                .HasPrecision(5, 2)
                .HasColumnName("metric_value");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.CampaignCreative).WithMany(p => p.CreativeScores)
                .HasForeignKey(d => d.CampaignCreativeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("creative_scores_campaign_creative_id_fkey");
        });

        modelBuilder.Entity<CampaignConversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_conversations_pkey");

            entity.ToTable("campaign_conversations");

            entity.HasIndex(e => e.CampaignId, "uq_campaign_conversations_campaign_id").IsUnique();
            entity.HasIndex(e => e.ClientUserId, "ix_campaign_conversations_client_user_id");
            entity.HasIndex(e => e.LastMessageAt, "ix_campaign_conversations_last_message_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.ClientUserId).HasColumnName("client_user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.LastMessageAt).HasColumnName("last_message_at");

            entity.HasOne(d => d.Campaign).WithOne(p => p.CampaignConversation)
                .HasForeignKey<CampaignConversation>(d => d.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_conversations_campaign_id_fkey");

            entity.HasOne(d => d.ClientUser).WithMany(p => p.CampaignConversations)
                .HasForeignKey(d => d.ClientUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("campaign_conversations_client_user_id_fkey");
        });

        modelBuilder.Entity<CampaignDeliveryReport>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_delivery_reports_pkey");

            entity.ToTable("campaign_delivery_reports");

            entity.HasIndex(e => e.CampaignId, "ix_campaign_delivery_reports_campaign_id");
            entity.HasIndex(e => e.ReportedAt, "ix_campaign_delivery_reports_reported_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.EvidenceAssetId).HasColumnName("evidence_asset_id");
            entity.Property(e => e.Headline)
                .HasMaxLength(200)
                .HasColumnName("headline");
            entity.Property(e => e.Impressions).HasColumnName("impressions");
            entity.Property(e => e.PlaysOrSpots).HasColumnName("plays_or_spots");
            entity.Property(e => e.ReportType)
                .HasMaxLength(50)
                .HasColumnName("report_type");
            entity.Property(e => e.ReportedAt).HasColumnName("reported_at");
            entity.Property(e => e.SpendDelivered)
                .HasPrecision(12, 2)
                .HasColumnName("spend_delivered");
            entity.Property(e => e.SupplierBookingId).HasColumnName("supplier_booking_id");
            entity.Property(e => e.Summary).HasColumnName("summary");

            entity.HasOne(d => d.Campaign).WithMany(p => p.CampaignDeliveryReports)
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_delivery_reports_campaign_id_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaign_delivery_reports_created_by_user_id_fkey");

            entity.HasOne(d => d.EvidenceAsset).WithMany()
                .HasForeignKey(d => d.EvidenceAssetId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaign_delivery_reports_evidence_asset_id_fkey");

            entity.HasOne(d => d.SupplierBooking).WithMany()
                .HasForeignKey(d => d.SupplierBookingId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaign_delivery_reports_supplier_booking_id_fkey");
        });

        modelBuilder.Entity<CampaignMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_messages_pkey");

            entity.ToTable("campaign_messages");

            entity.HasIndex(e => e.ConversationId, "ix_campaign_messages_conversation_id");
            entity.HasIndex(e => e.SenderUserId, "ix_campaign_messages_sender_user_id");
            entity.HasIndex(e => e.CreatedAt, "ix_campaign_messages_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.SenderUserId).HasColumnName("sender_user_id");
            entity.Property(e => e.SenderRole)
                .HasMaxLength(20)
                .HasColumnName("sender_role");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ReadByClientAt).HasColumnName("read_by_client_at");
            entity.Property(e => e.ReadByAgentAt).HasColumnName("read_by_agent_at");
            entity.Property(e => e.EmailNotificationSentAt).HasColumnName("email_notification_sent_at");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_messages_conversation_id_fkey");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.CampaignMessages)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("campaign_messages_sender_user_id_fkey");
        });

        modelBuilder.Entity<CampaignPauseWindow>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_pause_windows_pkey");

            entity.ToTable("campaign_pause_windows");

            entity.HasIndex(e => e.CampaignId, "ix_campaign_pause_windows_campaign_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.PausedDayCount)
                .HasDefaultValue(0)
                .HasColumnName("paused_day_count");
            entity.Property(e => e.PauseReason).HasColumnName("pause_reason");
            entity.Property(e => e.ResumeReason).HasColumnName("resume_reason");
            entity.Property(e => e.ResumedByUserId).HasColumnName("resumed_by_user_id");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");

            entity.HasOne(d => d.Campaign).WithMany(p => p.CampaignPauseWindows)
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_pause_windows_campaign_id_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaign_pause_windows_created_by_user_id_fkey");

            entity.HasOne(d => d.ResumedByUser).WithMany()
                .HasForeignKey(d => d.ResumedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaign_pause_windows_resumed_by_user_id_fkey");
        });

        modelBuilder.Entity<CampaignRecommendation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_recommendations_pkey");

            entity.ToTable("campaign_recommendations");

            entity.HasIndex(e => e.CampaignId, "ix_campaign_recommendations_campaign_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.GeneratedBy)
                .HasMaxLength(50)
                .HasColumnName("generated_by");
            entity.Property(e => e.Rationale).HasColumnName("rationale");
            entity.Property(e => e.RecommendationType)
                .HasMaxLength(50)
                .HasColumnName("recommendation_type");
            entity.Property(e => e.RevisionNumber)
                .HasDefaultValue(1)
                .HasColumnName("revision_number");
            entity.Property(e => e.PdfGeneratedAt).HasColumnName("pdf_generated_at");
            entity.Property(e => e.PdfStorageObjectKey).HasColumnName("pdf_storage_object_key");
            entity.Property(e => e.SentToClientAt).HasColumnName("sent_to_client_at");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'draft'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.Summary).HasColumnName("summary");
            entity.Property(e => e.TotalCost)
                .HasPrecision(12, 2)
                .HasColumnName("total_cost");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Campaign).WithMany(p => p.CampaignRecommendations)
                .HasForeignKey(d => d.CampaignId)
                .HasConstraintName("campaign_recommendations_campaign_id_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.CampaignRecommendations)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaign_recommendations_created_by_user_id_fkey");
        });

        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("email_verification_tokens_pkey");

            entity.ToTable("email_verification_tokens");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.TokenHash).HasColumnName("token_hash");
            entity.Property(e => e.UsedAt).HasColumnName("used_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.EmailVerificationTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("email_verification_tokens_user_id_fkey");
        });

        modelBuilder.Entity<IdentityProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("identity_profiles_pkey");

            entity.ToTable("identity_profiles");

            entity.HasIndex(e => e.SaIdNumber, "ix_identity_profiles_sa_id_number");

            entity.HasIndex(e => new { e.PassportNumber, e.PassportCountryIso2 }, "uq_identity_profiles_passport").IsUnique();

            entity.HasIndex(e => e.SaIdNumber, "uq_identity_profiles_sa_id_number").IsUnique();

            entity.HasIndex(e => e.UserId, "uq_identity_profiles_user_id").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.PassportCountryIso2)
                .HasMaxLength(2)
                .IsFixedLength()
                .HasColumnName("passport_country_iso2");
            entity.Property(e => e.PassportIssueDate).HasColumnName("passport_issue_date");
            entity.Property(e => e.PassportNumber)
                .HasMaxLength(50)
                .HasColumnName("passport_number");
            entity.Property(e => e.PassportValidUntil).HasColumnName("passport_valid_until");
            entity.Property(e => e.SaIdNumber)
                .HasMaxLength(20)
                .HasColumnName("sa_id_number");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithOne(p => p.IdentityProfile)
                .HasForeignKey<IdentityProfile>(d => d.UserId)
                .HasConstraintName("identity_profiles_user_id_fkey");
        });

        modelBuilder.Entity<NotificationReadReceipt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notification_read_receipts_pkey");

            entity.ToTable("notification_read_receipts");

            entity.HasIndex(e => e.UserId, "ix_notification_read_receipts_user_id");

            entity.HasIndex(e => new { e.UserId, e.NotificationId }, "uq_notification_read_receipts_user_notification")
                .IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.NotificationId)
                .HasMaxLength(200)
                .HasColumnName("notification_id");
            entity.Property(e => e.ReadAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("read_at");

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("notification_read_receipts_user_id_fkey");
        });

        modelBuilder.Entity<PackageBand>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("package_bands_pkey");

            entity.ToTable("package_bands");

            entity.HasIndex(e => e.Code, "package_bands_code_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.MaxBudget)
                .HasPrecision(12, 2)
                .HasColumnName("max_budget");
            entity.Property(e => e.MinBudget)
                .HasPrecision(12, 2)
                .HasColumnName("min_budget");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<PackageOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("package_orders_pkey");

            entity.ToTable("package_orders");

            entity.HasIndex(e => e.PackageBandId, "ix_package_orders_package_band_id");

            entity.HasIndex(e => e.PaymentStatus, "ix_package_orders_payment_status");

            entity.HasIndex(e => e.UserId, "ix_package_orders_user_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(12, 2)
                .HasColumnName("amount");
            entity.Property(e => e.AiStudioReserveAmount)
                .HasPrecision(12, 2)
                .HasDefaultValue(0m)
                .HasColumnName("ai_studio_reserve_amount");
            entity.Property(e => e.AiStudioReservePercent)
                .HasPrecision(8, 4)
                .HasDefaultValue(0.10m)
                .HasColumnName("ai_studio_reserve_percent");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.GatewayFeeRetainedAmount)
                .HasPrecision(12, 2)
                .HasDefaultValue(0m)
                .HasColumnName("gateway_fee_retained_amount");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'ZAR'::character varying")
                .HasColumnName("currency");
            entity.Property(e => e.PackageBandId).HasColumnName("package_band_id");
            entity.Property(e => e.PaymentProvider)
                .HasMaxLength(50)
                .HasColumnName("payment_provider");
            entity.Property(e => e.PaymentReference)
                .HasMaxLength(100)
                .HasColumnName("payment_reference");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .HasDefaultValueSql("'pending'::character varying")
                .HasColumnName("payment_status");
            entity.Property(e => e.PurchasedAt).HasColumnName("purchased_at");
            entity.Property(e => e.RefundProcessedAt).HasColumnName("refund_processed_at");
            entity.Property(e => e.RefundReason).HasColumnName("refund_reason");
            entity.Property(e => e.RefundStatus)
                .HasMaxLength(50)
                .HasDefaultValueSql("'none'::character varying")
                .HasColumnName("refund_status");
            entity.Property(e => e.RefundedAmount)
                .HasPrecision(12, 2)
                .HasDefaultValue(0m)
                .HasColumnName("refunded_amount");
            entity.Property(e => e.SelectedBudget)
                .HasPrecision(12, 2)
                .HasColumnName("selected_budget");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.PackageBand).WithMany(p => p.PackageOrders)
                .HasForeignKey(d => d.PackageBandId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("package_orders_package_band_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.PackageOrders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("package_orders_user_id_fkey");
        });

        modelBuilder.Entity<CampaignSupplierBooking>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_supplier_bookings_pkey");

            entity.ToTable("campaign_supplier_bookings");

            entity.HasIndex(e => e.CampaignId, "ix_campaign_supplier_bookings_campaign_id");
            entity.HasIndex(e => new { e.LiveFrom, e.LiveTo }, "ix_campaign_supplier_bookings_live_window");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.BookedAt).HasColumnName("booked_at");
            entity.Property(e => e.BookingStatus)
                .HasMaxLength(50)
                .HasDefaultValueSql("'planned'::character varying")
                .HasColumnName("booking_status");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.Channel)
                .HasMaxLength(50)
                .HasColumnName("channel");
            entity.Property(e => e.CommittedAmount)
                .HasPrecision(12, 2)
                .HasDefaultValue(0m)
                .HasColumnName("committed_amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.LiveFrom).HasColumnName("live_from");
            entity.Property(e => e.LiveTo).HasColumnName("live_to");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.ProofAssetId).HasColumnName("proof_asset_id");
            entity.Property(e => e.SupplierOrStation)
                .HasMaxLength(255)
                .HasColumnName("supplier_or_station");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Campaign).WithMany(p => p.CampaignSupplierBookings)
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_supplier_bookings_campaign_id_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaign_supplier_bookings_created_by_user_id_fkey");

            entity.HasOne(d => d.ProofAsset).WithMany()
                .HasForeignKey(d => d.ProofAssetId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaign_supplier_bookings_proof_asset_id_fkey");
        });

        modelBuilder.Entity<PricingSetting>(entity =>
        {
            entity.HasKey(e => e.PricingKey).HasName("pricing_settings_pkey");

            entity.ToTable("pricing_settings");

            entity.Property(e => e.PricingKey)
                .HasMaxLength(50)
                .HasColumnName("pricing_key");
            entity.Property(e => e.AiStudioReservePercent)
                .HasPrecision(8, 4)
                .HasColumnName("ai_studio_reserve_percent");
            entity.Property(e => e.OohMarkupPercent)
                .HasPrecision(8, 4)
                .HasColumnName("ooh_markup_percent");
            entity.Property(e => e.RadioMarkupPercent)
                .HasPrecision(8, 4)
                .HasColumnName("radio_markup_percent");
            entity.Property(e => e.TvMarkupPercent)
                .HasPrecision(8, 4)
                .HasColumnName("tv_markup_percent");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<RecommendationItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("recommendation_items_pkey");

            entity.ToTable("recommendation_items");

            entity.HasIndex(e => e.RecommendationId, "ix_recommendation_items_recommendation_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(255)
                .HasColumnName("display_name");
            entity.Property(e => e.InventoryItemId).HasColumnName("inventory_item_id");
            entity.Property(e => e.InventoryType)
                .HasMaxLength(50)
                .HasColumnName("inventory_type");
            entity.Property(e => e.MetadataJson)
                .HasColumnType("jsonb")
                .HasColumnName("metadata_json");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1)
                .HasColumnName("quantity");
            entity.Property(e => e.RecommendationId).HasColumnName("recommendation_id");
            entity.Property(e => e.TotalCost)
                .HasPrecision(12, 2)
                .HasColumnName("total_cost");
            entity.Property(e => e.UnitCost)
                .HasPrecision(12, 2)
                .HasColumnName("unit_cost");

            entity.HasOne(d => d.Recommendation).WithMany(p => p.RecommendationItems)
                .HasForeignKey(d => d.RecommendationId)
                .HasConstraintName("recommendation_items_recommendation_id_fkey");
        });

        modelBuilder.Entity<ConsentPreference>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("consent_preferences_pkey");

            entity.ToTable("consent_preferences");

            entity.HasIndex(e => e.BrowserId, "ix_consent_preferences_browser_id");

            entity.HasIndex(e => e.BrowserId, "uq_consent_preferences_browser_id").IsUnique();

            entity.HasIndex(e => e.UserId, "ix_consent_preferences_user_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AnalyticsCookies).HasColumnName("analytics_cookies");
            entity.Property(e => e.BrowserId)
                .HasMaxLength(200)
                .HasColumnName("browser_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.MarketingCookies).HasColumnName("marketing_cookies");
            entity.Property(e => e.NecessaryCookies)
                .HasDefaultValue(true)
                .HasColumnName("necessary_cookies");
            entity.Property(e => e.PrivacyAccepted).HasColumnName("privacy_accepted");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.ConsentPreferences)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("consent_preferences_user_id_fkey");
        });

        modelBuilder.Entity<FormOptionItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("form_option_items_pkey");

            entity.ToTable("form_option_items");

            entity.HasIndex(e => new { e.OptionSetKey, e.IsActive, e.SortOrder }, "ix_form_option_items_set_active_sort");

            entity.HasIndex(e => new { e.OptionSetKey, e.Value }, "uq_form_option_items_set_value").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.OptionSetKey)
                .HasMaxLength(100)
                .HasColumnName("option_set_key");
            entity.Property(e => e.Value)
                .HasMaxLength(100)
                .HasColumnName("value");
            entity.Property(e => e.Label)
                .HasMaxLength(200)
                .HasColumnName("label");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_accounts_pkey");

            entity.ToTable("user_accounts");

            entity.HasIndex(e => e.Email, "ix_user_accounts_email");

            entity.HasIndex(e => e.Email, "uq_user_accounts_email").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.EmailVerified).HasColumnName("email_verified");
            entity.Property(e => e.FullName)
                .HasMaxLength(200)
                .HasColumnName("full_name");
            entity.Property(e => e.IsSaCitizen).HasColumnName("is_sa_citizen");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .HasColumnName("phone");
            entity.Property(e => e.PhoneVerified).HasColumnName("phone_verified");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
