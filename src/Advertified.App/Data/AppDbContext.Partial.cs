using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Data;

public partial class AppDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CampaignRecommendation>(entity =>
        {
            entity.Property(e => e.RequestSnapshotJson)
                .HasColumnType("jsonb")
                .HasColumnName("request_snapshot_json");
            entity.Property(e => e.PolicySnapshotJson)
                .HasColumnType("jsonb")
                .HasColumnName("policy_snapshot_json");
            entity.Property(e => e.InventorySnapshotJson)
                .HasColumnType("jsonb")
                .HasColumnName("inventory_snapshot_json");
            entity.Property(e => e.InventoryBatchRefsJson)
                .HasColumnType("jsonb")
                .HasColumnName("inventory_batch_refs_json");
        });

        modelBuilder.Entity<RecommendationRunAudit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("recommendation_run_audits_pkey");

            entity.ToTable("recommendation_run_audits");

            entity.HasIndex(e => e.CampaignId, "ix_recommendation_run_audits_campaign_id");
            entity.HasIndex(e => e.RecommendationId, "ix_recommendation_run_audits_recommendation_id");
            entity.HasIndex(e => e.CreatedAt, "ix_recommendation_run_audits_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.RecommendationId).HasColumnName("recommendation_id");
            entity.Property(e => e.RecommendationType)
                .HasMaxLength(100)
                .HasColumnName("recommendation_type");
            entity.Property(e => e.RevisionNumber).HasColumnName("revision_number");
            entity.Property(e => e.RequestSnapshotJson)
                .HasColumnType("jsonb")
                .HasColumnName("request_snapshot_json");
            entity.Property(e => e.PolicySnapshotJson)
                .HasColumnType("jsonb")
                .HasColumnName("policy_snapshot_json");
            entity.Property(e => e.InventorySnapshotJson)
                .HasColumnType("jsonb")
                .HasColumnName("inventory_snapshot_json");
            entity.Property(e => e.InventoryBatchRefsJson)
                .HasColumnType("jsonb")
                .HasColumnName("inventory_batch_refs_json");
            entity.Property(e => e.CandidateCountsJson)
                .HasColumnType("jsonb")
                .HasColumnName("candidate_counts_json");
            entity.Property(e => e.RejectedCandidatesJson)
                .HasColumnType("jsonb")
                .HasColumnName("rejected_candidates_json");
            entity.Property(e => e.SelectedItemsJson)
                .HasColumnType("jsonb")
                .HasColumnName("selected_items_json");
            entity.Property(e => e.FallbackFlagsJson)
                .HasColumnType("jsonb")
                .HasColumnName("fallback_flags_json");
            entity.Property(e => e.BudgetUtilizationRatio)
                .HasPrecision(8, 4)
                .HasColumnName("budget_utilization_ratio");
            entity.Property(e => e.ManualReviewRequired).HasColumnName("manual_review_required");
            entity.Property(e => e.FinalRationale).HasColumnName("final_rationale");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(e => e.Campaign)
                .WithMany(e => e.RecommendationRunAudits)
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("recommendation_run_audits_campaign_id_fkey");

            entity.HasOne(e => e.Recommendation)
                .WithMany(e => e.RecommendationRunAudits)
                .HasForeignKey(e => e.RecommendationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("recommendation_run_audits_recommendation_id_fkey");
        });

        modelBuilder.Entity<InventoryImportBatch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inventory_import_batches_pkey");

            entity.ToTable("inventory_import_batches");

            entity.HasIndex(e => new { e.ChannelFamily, e.CreatedAt }, "ix_inventory_import_batches_channel_created");
            entity.HasIndex(e => new { e.ChannelFamily, e.IsActive }, "ix_inventory_import_batches_channel_active");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ChannelFamily)
                .HasMaxLength(50)
                .HasColumnName("channel_family");
            entity.Property(e => e.SourceType)
                .HasMaxLength(50)
                .HasColumnName("source_type");
            entity.Property(e => e.SourceIdentifier)
                .HasMaxLength(500)
                .HasColumnName("source_identifier");
            entity.Property(e => e.SourceChecksum)
                .HasMaxLength(128)
                .HasColumnName("source_checksum");
            entity.Property(e => e.RecordCount).HasColumnName("record_count");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasColumnName("status");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.MetadataJson)
                .HasColumnType("jsonb")
                .HasColumnName("metadata_json");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ActivatedAt).HasColumnName("activated_at");
        });

        modelBuilder.Entity<AdPlatformConnection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ad_platform_connections_pkey");

            entity.ToTable("ad_platform_connections");

            entity.HasIndex(e => e.OwnerUserId, "ix_ad_platform_connections_owner_user_id");
            entity.HasIndex(e => new { e.Provider, e.ExternalAccountId }, "uq_ad_platform_connections_provider_external_account").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.Provider)
                .HasMaxLength(40)
                .HasColumnName("provider");
            entity.Property(e => e.ExternalAccountId)
                .HasMaxLength(160)
                .HasColumnName("external_account_id");
            entity.Property(e => e.AccountName)
                .HasMaxLength(200)
                .HasColumnName("account_name");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasColumnName("status");
            entity.Property(e => e.AccessToken).HasColumnName("access_token");
            entity.Property(e => e.RefreshToken).HasColumnName("refresh_token");
            entity.Property(e => e.TokenExpiresAt).HasColumnName("token_expires_at");
            entity.Property(e => e.MetadataJson)
                .HasColumnType("jsonb")
                .HasColumnName("metadata_json");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.LastSyncedAt).HasColumnName("last_synced_at");
        });

        modelBuilder.Entity<CampaignAdPlatformLink>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_ad_platform_links_pkey");

            entity.ToTable("campaign_ad_platform_links");

            entity.HasIndex(e => e.CampaignId, "ix_campaign_ad_platform_links_campaign_id");
            entity.HasIndex(e => e.AdPlatformConnectionId, "ix_campaign_ad_platform_links_connection_id");
            entity.HasIndex(e => new { e.CampaignId, e.AdPlatformConnectionId }, "uq_campaign_ad_platform_links_campaign_connection").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.AdPlatformConnectionId).HasColumnName("ad_platform_connection_id");
            entity.Property(e => e.ExternalCampaignId)
                .HasMaxLength(160)
                .HasColumnName("external_campaign_id");
            entity.Property(e => e.IsPrimary).HasColumnName("is_primary");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasColumnName("status");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(e => e.Campaign)
                .WithMany()
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_ad_platform_links_campaign_id_fkey");

            entity.HasOne(e => e.AdPlatformConnection)
                .WithMany()
                .HasForeignKey(e => e.AdPlatformConnectionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_ad_platform_links_connection_id_fkey");
        });

        modelBuilder.Entity<CampaignChannelMetric>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_channel_metrics_pkey");

            entity.ToTable("campaign_channel_metrics");

            entity.HasIndex(e => e.CampaignId, "ix_campaign_channel_metrics_campaign_id");
            entity.HasIndex(e => e.MetricDate, "ix_campaign_channel_metrics_metric_date");
            entity.HasIndex(e => new { e.CampaignId, e.Channel, e.Provider, e.MetricDate }, "ux_campaign_channel_metrics_campaign_channel_provider_date")
                .IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.Channel)
                .HasMaxLength(60)
                .HasColumnName("channel");
            entity.Property(e => e.Provider)
                .HasMaxLength(80)
                .HasColumnName("provider");
            entity.Property(e => e.MetricDate).HasColumnName("metric_date");
            entity.Property(e => e.SpendZar)
                .HasPrecision(12, 2)
                .HasColumnName("spend_zar");
            entity.Property(e => e.Impressions).HasColumnName("impressions");
            entity.Property(e => e.Clicks).HasColumnName("clicks");
            entity.Property(e => e.Leads).HasColumnName("leads");
            entity.Property(e => e.AttributedRevenueZar)
                .HasPrecision(12, 2)
                .HasColumnName("attributed_revenue_zar");
            entity.Property(e => e.CplZar)
                .HasPrecision(12, 2)
                .HasColumnName("cpl_zar");
            entity.Property(e => e.Roas)
                .HasPrecision(12, 4)
                .HasColumnName("roas");
            entity.Property(e => e.SourceType)
                .HasMaxLength(50)
                .HasColumnName("source_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(e => e.Campaign)
                .WithMany(e => e.CampaignChannelMetrics)
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_channel_metrics_campaign_id_fkey");
        });

        modelBuilder.Entity<CampaignExecutionTask>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("campaign_execution_tasks_pkey");

            entity.ToTable("campaign_execution_tasks");

            entity.HasIndex(e => new { e.CampaignId, e.Status }, "ix_campaign_execution_tasks_campaign_status");
            entity.HasIndex(e => new { e.CampaignId, e.TaskKey }, "ux_campaign_execution_tasks_campaign_task_key")
                .IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.TaskKey)
                .HasMaxLength(80)
                .HasColumnName("task_key");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasColumnName("status");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.DueAt).HasColumnName("due_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(e => e.Campaign)
                .WithMany(e => e.CampaignExecutionTasks)
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("campaign_execution_tasks_campaign_id_fkey");
        });

        modelBuilder.Entity<AgentAreaAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("agent_area_assignments_pkey");

            entity.ToTable("agent_area_assignments");

            entity.HasIndex(e => e.AreaCode, "uq_agent_area_assignments_area_code").IsUnique();
            entity.HasIndex(e => new { e.AgentUserId, e.AreaCode }, "uq_agent_area_assignments_agent_user_id_area_code").IsUnique();
            entity.HasIndex(e => e.AgentUserId, "ix_agent_area_assignments_agent_user_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AgentUserId).HasColumnName("agent_user_id");
            entity.Property(e => e.AreaCode)
                .HasMaxLength(50)
                .HasColumnName("area_code");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.HasOne(e => e.AgentUser)
                .WithMany()
                .HasForeignKey(e => e.AgentUserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("agent_area_assignments_agent_user_id_fkey");
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.Property(e => e.Role)
                .HasColumnType("user_role")
                .HasColumnName("role");

            entity.Property(e => e.AccountStatus)
                .HasColumnType("account_status")
                .HasColumnName("account_status");
        });

        modelBuilder.Entity<BusinessProfile>(entity =>
        {
            entity.Property(e => e.VerificationStatus)
                .HasColumnType("verification_status")
                .HasColumnName("verification_status");
        });

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasIndex(e => e.AssignedAgentUserId, "ix_campaigns_assigned_agent_user_id");
            entity.HasIndex(e => e.ProspectDispositionClosedByUserId, "ix_campaigns_prospect_disposition_closed_by_user_id");
            entity.HasIndex(e => e.ProspectLeadId, "ix_campaigns_prospect_lead_id");
            entity.HasIndex(e => e.ProspectDispositionStatus, "ix_campaigns_prospect_disposition_status");

            entity.Property(e => e.AssignedAgentUserId).HasColumnName("assigned_agent_user_id");
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");
            entity.Property(e => e.AssignmentEmailSentAt).HasColumnName("assignment_email_sent_at");
            entity.Property(e => e.AgentWorkStartedEmailSentAt).HasColumnName("agent_work_started_email_sent_at");
            entity.Property(e => e.ProspectLeadId).HasColumnName("prospect_lead_id");
            entity.Property(e => e.ProspectDispositionClosedAt).HasColumnName("prospect_disposition_closed_at");
            entity.Property(e => e.ProspectDispositionClosedByUserId).HasColumnName("prospect_disposition_closed_by_user_id");
            entity.Property(e => e.ProspectDispositionNotes).HasColumnName("prospect_disposition_notes");
            entity.Property(e => e.ProspectDispositionReason)
                .HasMaxLength(100)
                .HasColumnName("prospect_disposition_reason");
            entity.Property(e => e.ProspectDispositionStatus)
                .HasMaxLength(20)
                .HasColumnName("prospect_disposition_status");
            entity.Property(e => e.RecommendationReadyEmailSentAt).HasColumnName("recommendation_ready_email_sent_at");

            entity.HasOne(e => e.AssignedAgentUser)
                .WithMany()
                .HasForeignKey(e => e.AssignedAgentUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaigns_assigned_agent_user_id_fkey");

            entity.HasOne(e => e.ProspectDispositionClosedByUser)
                .WithMany()
                .HasForeignKey(e => e.ProspectDispositionClosedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaigns_prospect_disposition_closed_by_user_id_fkey");

            entity.HasOne(e => e.ProspectLead)
                .WithMany(e => e.Campaigns)
                .HasForeignKey(e => e.ProspectLeadId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaigns_prospect_lead_id_fkey");
        });

        modelBuilder.Entity<PackageOrder>(entity =>
        {
            entity.HasIndex(e => e.ProspectLeadId, "ix_package_orders_prospect_lead_id");
            entity.Property(e => e.ProspectLeadId).HasColumnName("prospect_lead_id");

            entity.HasOne(e => e.ProspectLead)
                .WithMany(e => e.PackageOrders)
                .HasForeignKey(e => e.ProspectLeadId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("package_orders_prospect_lead_id_fkey");
        });

        modelBuilder.Entity<ProspectLead>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("prospect_leads_pkey");

            entity.ToTable("prospect_leads");

            entity.HasIndex(e => e.Email, "ix_prospect_leads_email");
            entity.HasIndex(e => e.NormalizedEmail, "ix_prospect_leads_normalized_email");
            entity.HasIndex(e => e.NormalizedPhone, "ix_prospect_leads_normalized_phone");
            entity.HasIndex(e => e.ClaimedUserId, "ix_prospect_leads_claimed_user_id");
            entity.HasIndex(e => e.OwnerAgentUserId, "ix_prospect_leads_owner_agent_user_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.FullName)
                .HasMaxLength(200)
                .HasColumnName("full_name");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.NormalizedEmail)
                .HasMaxLength(255)
                .HasColumnName("normalized_email");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .HasColumnName("phone");
            entity.Property(e => e.NormalizedPhone)
                .HasMaxLength(30)
                .HasColumnName("normalized_phone");
            entity.Property(e => e.Source)
                .HasMaxLength(50)
                .HasColumnName("source");
            entity.Property(e => e.ClaimedUserId).HasColumnName("claimed_user_id");
            entity.Property(e => e.OwnerAgentUserId).HasColumnName("owner_agent_user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(e => e.ClaimedUser)
                .WithMany()
                .HasForeignKey(e => e.ClaimedUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("prospect_leads_claimed_user_id_fkey");

            entity.HasOne(e => e.OwnerAgentUser)
                .WithMany()
                .HasForeignKey(e => e.OwnerAgentUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("prospect_leads_owner_agent_user_id_fkey");
        });

        modelBuilder.Entity<IdentityProfile>(entity =>
        {
            entity.Property(e => e.IdentityType)
                .HasColumnType("identity_type")
                .HasColumnName("identity_type");

            entity.Property(e => e.VerificationStatus)
                .HasColumnType("verification_status")
                .HasColumnName("verification_status");
        });

        modelBuilder.Entity<EmailTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("email_templates_pkey");

            entity.ToTable("email_templates");

            entity.HasIndex(e => e.TemplateName, "uq_email_templates_template_name").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.TemplateName)
                .HasMaxLength(120)
                .HasColumnName("template_name");
            entity.Property(e => e.SubjectTemplate)
                .HasColumnName("subject_template");
            entity.Property(e => e.BodyHtmlTemplate)
                .HasColumnName("body_html_template");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("created_at_utc");
            entity.Property(e => e.UpdatedAtUtc)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("updated_at_utc");
        });

        modelBuilder.Entity<Lead>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("leads_pkey");

            entity.ToTable("leads");

            entity.HasIndex(e => new { e.Name, e.Location }, "ix_leads_name_location");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.Website)
                .HasMaxLength(500)
                .HasColumnName("website");
            entity.Property(e => e.Location)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("location");
            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("category");
            entity.Property(e => e.Source)
                .IsRequired()
                .HasMaxLength(100)
                .HasDefaultValue("manual")
                .HasColumnName("source");
            entity.Property(e => e.SourceReference)
                .HasMaxLength(500)
                .HasColumnName("source_reference");
            entity.Property(e => e.LastDiscoveredAt)
                .HasColumnName("last_discovered_at");
            entity.Property(e => e.Latitude)
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnName("longitude");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("created_at");
        });

        modelBuilder.Entity<LeadAction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("lead_actions_pkey");

            entity.ToTable("lead_actions");

            entity.HasIndex(e => e.LeadId, "ix_lead_actions_lead_id");
            entity.HasIndex(e => e.Status, "ix_lead_actions_status");
            entity.HasIndex(e => e.AssignedAgentUserId, "ix_lead_actions_assigned_agent_user_id");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(e => e.LeadId)
                .HasColumnName("lead_id");
            entity.Property(e => e.LeadInsightId)
                .HasColumnName("lead_insight_id");
            entity.Property(e => e.ActionType)
                .HasMaxLength(50)
                .HasColumnName("action_type");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.Description)
                .HasColumnName("description");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("open")
                .HasColumnName("status");
            entity.Property(e => e.Priority)
                .HasMaxLength(30)
                .HasDefaultValue("medium")
                .HasColumnName("priority");
            entity.Property(e => e.AssignedAgentUserId)
                .HasColumnName("assigned_agent_user_id");
            entity.Property(e => e.AssignedAt)
                .HasColumnName("assigned_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("created_at");
            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            entity.HasOne(e => e.Lead)
                .WithMany(e => e.Actions)
                .HasForeignKey(e => e.LeadId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("lead_actions_lead_id_fkey");

            entity.HasOne(e => e.LeadInsight)
                .WithMany(e => e.Actions)
                .HasForeignKey(e => e.LeadInsightId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("lead_actions_lead_insight_id_fkey");

            entity.HasOne(e => e.AssignedAgentUser)
                .WithMany()
                .HasForeignKey(e => e.AssignedAgentUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("lead_actions_assigned_agent_user_id_fkey");
        });

        modelBuilder.Entity<LeadInteraction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("lead_interactions_pkey");

            entity.ToTable("lead_interactions");

            entity.HasIndex(e => e.LeadId, "ix_lead_interactions_lead_id");
            entity.HasIndex(e => e.CreatedAt, "ix_lead_interactions_created_at");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(e => e.LeadId)
                .HasColumnName("lead_id");
            entity.Property(e => e.LeadActionId)
                .HasColumnName("lead_action_id");
            entity.Property(e => e.InteractionType)
                .HasMaxLength(50)
                .HasColumnName("interaction_type");
            entity.Property(e => e.Notes)
                .HasColumnName("notes");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("created_at");

            entity.HasOne(e => e.Lead)
                .WithMany(e => e.Interactions)
                .HasForeignKey(e => e.LeadId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("lead_interactions_lead_id_fkey");

            entity.HasOne(e => e.LeadAction)
                .WithMany(e => e.Interactions)
                .HasForeignKey(e => e.LeadActionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("lead_interactions_lead_action_id_fkey");
        });

        modelBuilder.Entity<LeadInsight>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("lead_insights_pkey");

            entity.ToTable("lead_insights");

            entity.HasIndex(e => e.LeadId, "ix_lead_insights_lead_id");
            entity.HasIndex(e => e.CreatedAt, "ix_lead_insights_created_at");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(e => e.LeadId)
                .HasColumnName("lead_id");
            entity.Property(e => e.SignalId)
                .HasColumnName("signal_id");
            entity.Property(e => e.TrendSummary)
                .HasColumnName("trend_summary");
            entity.Property(e => e.ScoreSnapshot)
                .HasColumnName("score_snapshot");
            entity.Property(e => e.IntentLevelSnapshot)
                .HasMaxLength(20)
                .HasColumnName("intent_level_snapshot");
            entity.Property(e => e.Text)
                .HasColumnName("text");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("created_at");

            entity.HasOne(e => e.Lead)
                .WithMany(e => e.Insights)
                .HasForeignKey(e => e.LeadId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("lead_insights_lead_id_fkey");

            entity.HasOne(e => e.Signal)
                .WithMany(e => e.Insights)
                .HasForeignKey(e => e.SignalId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("lead_insights_signal_id_fkey");
        });

        modelBuilder.Entity<Signal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("signals_pkey");

            entity.ToTable("signals");

            entity.HasIndex(e => e.LeadId, "ix_signals_lead_id");
            entity.HasIndex(e => e.CreatedAt, "ix_signals_created_at");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(e => e.LeadId)
                .HasColumnName("lead_id");
            entity.Property(e => e.HasPromo)
                .HasColumnName("has_promo");
            entity.Property(e => e.HasMetaAds)
                .HasColumnName("has_meta_ads");
            entity.Property(e => e.WebsiteUpdatedRecently)
                .HasColumnName("website_updated_recently");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("created_at");

            entity.HasOne(e => e.Lead)
                .WithMany(e => e.Signals)
                .HasForeignKey(e => e.LeadId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("signals_lead_id_fkey");
        });

        modelBuilder.Entity<LeadSignalEvidence>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("lead_signal_evidence_pkey");

            entity.ToTable("lead_signal_evidence");

            entity.HasIndex(e => e.LeadId, "ix_lead_signal_evidence_lead_id");
            entity.HasIndex(e => e.SignalId, "ix_lead_signal_evidence_signal_id");
            entity.HasIndex(e => new { e.LeadId, e.Channel, e.CreatedAt }, "ix_lead_signal_evidence_lead_channel_created");
            entity.HasIndex(
                    e => new
                    {
                        e.SignalId,
                        e.Channel,
                        e.SignalType,
                        e.Source,
                        e.EvidenceUrl,
                        e.Value
                    },
                    "ux_lead_signal_evidence_signal_unique")
                .IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(e => e.LeadId).HasColumnName("lead_id");
            entity.Property(e => e.SignalId).HasColumnName("signal_id");
            entity.Property(e => e.Channel)
                .HasMaxLength(40)
                .HasColumnName("channel");
            entity.Property(e => e.SignalType)
                .HasMaxLength(80)
                .HasColumnName("signal_type");
            entity.Property(e => e.Source)
                .HasMaxLength(80)
                .HasColumnName("source");
            entity.Property(e => e.Confidence)
                .HasMaxLength(30)
                .HasColumnName("confidence");
            entity.Property(e => e.Weight).HasColumnName("weight");
            entity.Property(e => e.ReliabilityMultiplier)
                .HasPrecision(6, 4)
                .HasColumnName("reliability_multiplier");
            entity.Property(e => e.FreshnessMultiplier)
                .HasPrecision(6, 4)
                .HasColumnName("freshness_multiplier");
            entity.Property(e => e.EffectiveWeight)
                .HasPrecision(8, 2)
                .HasColumnName("effective_weight");
            entity.Property(e => e.IsPositive).HasColumnName("is_positive");
            entity.Property(e => e.ObservedAt).HasColumnName("observed_at");
            entity.Property(e => e.EvidenceUrl).HasColumnName("evidence_url");
            entity.Property(e => e.Value).HasColumnName("value");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("created_at");

            entity.HasOne(e => e.Lead)
                .WithMany(e => e.SignalEvidences)
                .HasForeignKey(e => e.LeadId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("lead_signal_evidence_lead_id_fkey");

            entity.HasOne(e => e.Signal)
                .WithMany(e => e.Evidences)
                .HasForeignKey(e => e.SignalId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("lead_signal_evidence_signal_id_fkey");
        });

        modelBuilder.Entity<PackageBandProfile>(entity =>
        {
            entity.HasKey(e => e.PackageBandId).HasName("package_band_profiles_pkey");

            entity.ToTable("package_band_profiles");

            entity.Property(e => e.PackageBandId).HasColumnName("package_band_id");
            entity.Property(e => e.AudienceFit).HasColumnName("audience_fit");
            entity.Property(e => e.BenefitsJson)
                .HasColumnType("jsonb")
                .HasColumnName("benefits_json");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IncludeRadio)
                .HasMaxLength(20)
                .HasColumnName("include_radio");
            entity.Property(e => e.IncludeTv)
                .HasMaxLength(20)
                .HasColumnName("include_tv");
            entity.Property(e => e.IsRecommended).HasColumnName("is_recommended");
            entity.Property(e => e.LeadTimeLabel)
                .HasMaxLength(100)
                .HasColumnName("lead_time_label");
            entity.Property(e => e.PackagePurpose).HasColumnName("package_purpose");
            entity.Property(e => e.QuickBenefit).HasColumnName("quick_benefit");
            entity.Property(e => e.RecommendedSpend)
                .HasPrecision(12, 2)
                .HasColumnName("recommended_spend");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<PackageBandPreviewTier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("package_band_preview_tiers_pkey");

            entity.ToTable("package_band_preview_tiers");

            entity.HasIndex(e => new { e.PackageBandId, e.TierCode }, "uq_package_band_preview_tiers_band_tier").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IndicativeMixJson)
                .HasColumnType("jsonb")
                .HasColumnName("indicative_mix_json");
            entity.Property(e => e.PackageBandId).HasColumnName("package_band_id");
            entity.Property(e => e.TierCode)
                .HasMaxLength(20)
                .HasColumnName("tier_code");
            entity.Property(e => e.TierLabel)
                .HasMaxLength(120)
                .HasColumnName("tier_label");
            entity.Property(e => e.TypicalInclusionsJson)
                .HasColumnType("jsonb")
                .HasColumnName("typical_inclusions_json");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<PackageBandAiEntitlement>(entity =>
        {
            entity.HasKey(e => e.PackageBandId).HasName("package_band_ai_entitlements_pkey");

            entity.ToTable("package_band_ai_entitlements");

            entity.Property(e => e.PackageBandId).HasColumnName("package_band_id");
            entity.Property(e => e.MaxAdVariants).HasColumnName("max_ad_variants");
            entity.Property(e => e.AllowedAdPlatformsJson)
                .HasColumnType("jsonb")
                .HasColumnName("allowed_ad_platforms_json");
            entity.Property(e => e.AllowAdMetricsSync).HasColumnName("allow_ad_metrics_sync");
            entity.Property(e => e.AllowAdAutoOptimize).HasColumnName("allow_ad_auto_optimize");
            entity.Property(e => e.AllowedVoicePackTiersJson)
                .HasColumnType("jsonb")
                .HasColumnName("allowed_voice_pack_tiers_json");
            entity.Property(e => e.MaxAdRegenerations).HasColumnName("max_ad_regenerations");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<InvoiceIssuerProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoice_issuer_profiles_pkey");

            entity.ToTable("invoice_issuer_profiles");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.ContactEmail)
                .HasMaxLength(255)
                .HasColumnName("contact_email");
            entity.Property(e => e.ContactPhone)
                .HasMaxLength(50)
                .HasColumnName("contact_phone");
            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("created_at_utc");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.LegalName)
                .HasMaxLength(200)
                .HasColumnName("legal_name");
            entity.Property(e => e.LogoPath).HasColumnName("logo_path");
            entity.Property(e => e.RegistrationNumber)
                .HasMaxLength(50)
                .HasColumnName("registration_number");
            entity.Property(e => e.UpdatedAtUtc)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("updated_at_utc");
            entity.Property(e => e.VatNumber)
                .HasMaxLength(50)
                .HasColumnName("vat_number");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoices_pkey");

            entity.ToTable("invoices");

            entity.HasIndex(e => e.InvoiceNumber, "uq_invoices_invoice_number").IsUnique();
            entity.HasIndex(e => e.PackageOrderId, "uq_invoices_package_order_id").IsUnique();
            entity.HasIndex(e => e.Status, "ix_invoices_status");
            entity.HasIndex(e => e.Provider, "ix_invoices_provider");
            entity.HasIndex(e => e.UserId, "ix_invoices_user_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.CampaignName)
                .HasMaxLength(200)
                .HasColumnName("campaign_name");
            entity.Property(e => e.CompanyAddress).HasColumnName("company_address");
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.CompanyName)
                .HasMaxLength(200)
                .HasColumnName("company_name");
            entity.Property(e => e.CompanyRegistrationNumber)
                .HasMaxLength(50)
                .HasColumnName("company_registration_number");
            entity.Property(e => e.CompanyVatNumber)
                .HasMaxLength(50)
                .HasColumnName("company_vat_number");
            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("created_at_utc");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'ZAR'::character varying")
                .HasColumnName("currency");
            entity.Property(e => e.CustomerAddress).HasColumnName("customer_address");
            entity.Property(e => e.CustomerEmail)
                .HasMaxLength(255)
                .HasColumnName("customer_email");
            entity.Property(e => e.CustomerName)
                .HasMaxLength(200)
                .HasColumnName("customer_name");
            entity.Property(e => e.DueAtUtc).HasColumnName("due_at_utc");
            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(50)
                .HasColumnName("invoice_number");
            entity.Property(e => e.InvoiceType)
                .HasMaxLength(50)
                .HasColumnName("invoice_type");
            entity.Property(e => e.PackageName)
                .HasMaxLength(100)
                .HasColumnName("package_name");
            entity.Property(e => e.PackageOrderId).HasColumnName("package_order_id");
            entity.Property(e => e.PaidAtUtc).HasColumnName("paid_at_utc");
            entity.Property(e => e.PaymentReference)
                .HasMaxLength(200)
                .HasColumnName("payment_reference");
            entity.Property(e => e.Provider)
                .HasMaxLength(50)
                .HasColumnName("provider");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.StorageObjectKey).HasColumnName("storage_object_key");
            entity.Property(e => e.SupportingDocumentStorageObjectKey).HasColumnName("supporting_document_storage_object_key");
            entity.Property(e => e.SupportingDocumentFileName)
                .HasMaxLength(255)
                .HasColumnName("supporting_document_file_name");
            entity.Property(e => e.SupportingDocumentUploadedAtUtc).HasColumnName("supporting_document_uploaded_at_utc");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(12, 2)
                .HasColumnName("total_amount");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Campaign).WithMany()
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("invoices_campaign_id_fkey");

            entity.HasOne(d => d.Company).WithMany()
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("invoices_company_id_fkey");

            entity.HasOne(d => d.PackageOrder).WithOne(p => p.Invoice)
                .HasForeignKey<Invoice>(d => d.PackageOrderId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("invoices_package_order_id_fkey");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("invoices_user_id_fkey");
        });

        modelBuilder.Entity<LegalDocument>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("legal_documents_pkey");

            entity.ToTable("legal_documents");

            entity.HasIndex(e => e.DocumentKey, "uq_legal_documents_document_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.BodyJson)
                .HasColumnType("jsonb")
                .HasColumnName("body_json");
            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("created_at_utc");
            entity.Property(e => e.DocumentKey)
                .HasMaxLength(120)
                .HasColumnName("document_key");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAtUtc)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("updated_at_utc");
            entity.Property(e => e.VersionLabel)
                .HasMaxLength(50)
                .HasColumnName("version_label");
        });

        modelBuilder.Entity<InvoiceLineItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoice_line_items_pkey");

            entity.ToTable("invoice_line_items");

            entity.HasIndex(e => e.InvoiceId, "ix_invoice_line_items_invoice_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("timezone('utc', now())")
                .HasColumnName("created_at_utc");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.LineType)
                .HasMaxLength(100)
                .HasColumnName("line_type");
            entity.Property(e => e.Quantity)
                .HasPrecision(12, 2)
                .HasColumnName("quantity");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.SubtotalAmount)
                .HasPrecision(12, 2)
                .HasColumnName("subtotal_amount");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(12, 2)
                .HasColumnName("total_amount");
            entity.Property(e => e.UnitAmount)
                .HasPrecision(12, 2)
                .HasColumnName("unit_amount");
            entity.Property(e => e.VatAmount)
                .HasPrecision(12, 2)
                .HasColumnName("vat_amount");

            entity.HasOne(d => d.Invoice).WithMany(p => p.LineItems)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("invoice_line_items_invoice_id_fkey");
        });

        modelBuilder.Entity<PaymentProviderRequestAudit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payment_provider_requests_pkey");

            entity.ToTable("payment_provider_requests");

            entity.HasIndex(e => e.PackageOrderId, "ix_payment_provider_requests_package_order_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EventType)
                .HasMaxLength(100)
                .HasColumnName("event_type");
            entity.Property(e => e.ExternalReference)
                .HasMaxLength(200)
                .HasColumnName("external_reference");
            entity.Property(e => e.PackageOrderId).HasColumnName("package_order_id");
            entity.Property(e => e.Provider)
                .HasMaxLength(50)
                .HasColumnName("provider");
            entity.Property(e => e.RequestBodyJson).HasColumnName("request_body_json");
            entity.Property(e => e.RequestHeadersJson)
                .HasColumnType("jsonb")
                .HasColumnName("request_headers_json");
            entity.Property(e => e.RequestUrl).HasColumnName("request_url");
            entity.Property(e => e.ResponseBodyText).HasColumnName("response_body_text");
            entity.Property(e => e.ResponseHeadersJson)
                .HasColumnType("jsonb")
                .HasColumnName("response_headers_json");
            entity.Property(e => e.ResponseStatusCode).HasColumnName("response_status_code");

            entity.HasOne(d => d.PackageOrder).WithMany()
                .HasForeignKey(d => d.PackageOrderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("payment_provider_requests_package_order_id_fkey");
        });

        modelBuilder.Entity<PaymentProviderWebhookAudit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payment_provider_webhooks_pkey");

            entity.ToTable("payment_provider_webhooks");

            entity.HasIndex(e => e.PackageOrderId, "ix_payment_provider_webhooks_package_order_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.BodyJson).HasColumnName("body_json");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.HeadersJson)
                .HasColumnType("jsonb")
                .HasColumnName("headers_json");
            entity.Property(e => e.PackageOrderId).HasColumnName("package_order_id");
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
            entity.Property(e => e.ProcessedMessage).HasColumnName("processed_message");
            entity.Property(e => e.ProcessedStatus)
                .HasMaxLength(50)
                .HasColumnName("processed_status");
            entity.Property(e => e.Provider)
                .HasMaxLength(50)
                .HasColumnName("provider");
            entity.Property(e => e.WebhookPath)
                .HasMaxLength(200)
                .HasColumnName("webhook_path");

            entity.HasOne(d => d.PackageOrder).WithMany()
                .HasForeignKey(d => d.PackageOrderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("payment_provider_webhooks_package_order_id_fkey");
        });

        modelBuilder.Entity<ChangeAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("change_audit_log_pkey");

            entity.ToTable("change_audit_log");

            entity.HasIndex(e => e.CreatedAt, "ix_change_audit_log_created_at");
            entity.HasIndex(e => e.Scope, "ix_change_audit_log_scope");
            entity.HasIndex(e => e.ActorUserId, "ix_change_audit_log_actor_user_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ActorEmail)
                .HasMaxLength(255)
                .HasColumnName("actor_email");
            entity.Property(e => e.ActorName)
                .HasMaxLength(200)
                .HasColumnName("actor_name");
            entity.Property(e => e.ActorRole)
                .HasMaxLength(50)
                .HasColumnName("actor_role");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .HasColumnName("action");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EntityId)
                .HasMaxLength(200)
                .HasColumnName("entity_id");
            entity.Property(e => e.EntityLabel)
                .HasMaxLength(255)
                .HasColumnName("entity_label");
            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .HasColumnName("entity_type");
            entity.Property(e => e.MetadataJson)
                .HasColumnType("jsonb")
                .HasColumnName("metadata_json");
            entity.Property(e => e.Scope)
                .HasMaxLength(50)
                .HasColumnName("scope");
            entity.Property(e => e.Summary).HasColumnName("summary");

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(e => e.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("change_audit_log_actor_user_id_fkey");
        });

        modelBuilder.Entity<EmailDeliveryProviderSetting>(entity =>
        {
            entity.HasKey(e => e.ProviderKey).HasName("email_delivery_provider_settings_pkey");

            entity.ToTable("email_delivery_provider_settings");

            entity.Property(e => e.ProviderKey)
                .HasMaxLength(50)
                .HasColumnName("provider_key");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(120)
                .HasColumnName("display_name");
            entity.Property(e => e.WebhookEnabled).HasColumnName("webhook_enabled");
            entity.Property(e => e.WebhookSigningSecret).HasColumnName("webhook_signing_secret");
            entity.Property(e => e.WebhookEndpointPath)
                .HasMaxLength(200)
                .HasColumnName("webhook_endpoint_path");
            entity.Property(e => e.AllowedEventTypesJson)
                .HasColumnType("jsonb")
                .HasColumnName("allowed_event_types_json");
            entity.Property(e => e.MaxSignatureAgeSeconds).HasColumnName("max_signature_age_seconds");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<EmailDeliveryMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("email_delivery_messages_pkey");

            entity.ToTable("email_delivery_messages");

            entity.HasIndex(e => new { e.CampaignId, e.CreatedAt }).HasDatabaseName("ix_email_delivery_messages_campaign_id");
            entity.HasIndex(e => e.RecipientEmail).HasDatabaseName("ix_email_delivery_messages_recipient_email");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ProviderKey)
                .HasMaxLength(50)
                .HasColumnName("provider_key");
            entity.Property(e => e.TemplateName)
                .HasMaxLength(120)
                .HasColumnName("template_name");
            entity.Property(e => e.SenderKey)
                .HasMaxLength(50)
                .HasColumnName("sender_key");
            entity.Property(e => e.DeliveryPurpose)
                .HasMaxLength(80)
                .HasColumnName("delivery_purpose");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .HasColumnName("status");
            entity.Property(e => e.FromAddress)
                .HasMaxLength(255)
                .HasColumnName("from_address");
            entity.Property(e => e.RecipientEmail)
                .HasMaxLength(255)
                .HasColumnName("recipient_email");
            entity.Property(e => e.Subject)
                .HasMaxLength(500)
                .HasColumnName("subject");
            entity.Property(e => e.CampaignId).HasColumnName("campaign_id");
            entity.Property(e => e.RecommendationId).HasColumnName("recommendation_id");
            entity.Property(e => e.RecommendationRevisionNumber).HasColumnName("recommendation_revision_number");
            entity.Property(e => e.RecipientUserId).HasColumnName("recipient_user_id");
            entity.Property(e => e.ProspectLeadId).HasColumnName("prospect_lead_id");
            entity.Property(e => e.ProviderMessageId)
                .HasMaxLength(120)
                .HasColumnName("provider_message_id");
            entity.Property(e => e.ProviderBroadcastId)
                .HasMaxLength(120)
                .HasColumnName("provider_broadcast_id");
            entity.Property(e => e.LatestEventType)
                .HasMaxLength(80)
                .HasColumnName("latest_event_type");
            entity.Property(e => e.LatestEventAt).HasColumnName("latest_event_at");
            entity.Property(e => e.AcceptedAt).HasColumnName("accepted_at");
            entity.Property(e => e.DeliveredAt).HasColumnName("delivered_at");
            entity.Property(e => e.OpenedAt).HasColumnName("opened_at");
            entity.Property(e => e.ClickedAt).HasColumnName("clicked_at");
            entity.Property(e => e.ComplainedAt).HasColumnName("complained_at");
            entity.Property(e => e.BouncedAt).HasColumnName("bounced_at");
            entity.Property(e => e.FailedAt).HasColumnName("failed_at");
            entity.Property(e => e.ArchivedAt).HasColumnName("archived_at");
            entity.Property(e => e.ArchivedPath).HasColumnName("archived_path");
            entity.Property(e => e.LastError).HasColumnName("last_error");
            entity.Property(e => e.MetadataJson)
                .HasColumnType("jsonb")
                .HasColumnName("metadata_json");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(e => e.Campaign)
                .WithMany(e => e.EmailDeliveryMessages)
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("email_delivery_messages_campaign_id_fkey");

            entity.HasOne(e => e.Recommendation)
                .WithMany(e => e.EmailDeliveryMessages)
                .HasForeignKey(e => e.RecommendationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("email_delivery_messages_recommendation_id_fkey");

            entity.HasOne(e => e.RecipientUser)
                .WithMany(e => e.EmailDeliveryMessages)
                .HasForeignKey(e => e.RecipientUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("email_delivery_messages_recipient_user_id_fkey");

            entity.HasOne(e => e.ProspectLead)
                .WithMany()
                .HasForeignKey(e => e.ProspectLeadId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("email_delivery_messages_prospect_lead_id_fkey");
        });

        modelBuilder.Entity<EmailDeliveryEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("email_delivery_events_pkey");

            entity.ToTable("email_delivery_events");

            entity.HasIndex(e => new { e.EmailDeliveryMessageId, e.EventCreatedAt }).HasDatabaseName("ix_email_delivery_events_message_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ProviderKey)
                .HasMaxLength(50)
                .HasColumnName("provider_key");
            entity.Property(e => e.EmailDeliveryMessageId).HasColumnName("email_delivery_message_id");
            entity.Property(e => e.ProviderWebhookMessageId)
                .HasMaxLength(120)
                .HasColumnName("provider_webhook_message_id");
            entity.Property(e => e.ProviderMessageId)
                .HasMaxLength(120)
                .HasColumnName("provider_message_id");
            entity.Property(e => e.ProviderEventType)
                .HasMaxLength(80)
                .HasColumnName("provider_event_type");
            entity.Property(e => e.RecipientEmail)
                .HasMaxLength(255)
                .HasColumnName("recipient_email");
            entity.Property(e => e.EventCreatedAt).HasColumnName("event_created_at");
            entity.Property(e => e.ReceivedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("received_at");
            entity.Property(e => e.ProcessingStatus)
                .HasMaxLength(40)
                .HasColumnName("processing_status");
            entity.Property(e => e.ProcessingNotes).HasColumnName("processing_notes");
            entity.Property(e => e.PayloadJson)
                .HasColumnType("jsonb")
                .HasColumnName("payload_json");

            entity.HasOne(e => e.EmailDeliveryMessage)
                .WithMany(e => e.Events)
                .HasForeignKey(e => e.EmailDeliveryMessageId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("email_delivery_events_email_delivery_message_id_fkey");
        });

        modelBuilder.Entity<EmailDeliveryWebhookAudit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("email_delivery_webhook_audits_pkey");

            entity.ToTable("email_delivery_webhook_audits");

            entity.HasIndex(e => new { e.ProviderKey, e.CreatedAt }).HasDatabaseName("ix_email_delivery_webhook_audits_provider_created");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ProviderKey)
                .HasMaxLength(50)
                .HasColumnName("provider_key");
            entity.Property(e => e.RequestPath)
                .HasMaxLength(200)
                .HasColumnName("request_path");
            entity.Property(e => e.WebhookMessageId)
                .HasMaxLength(120)
                .HasColumnName("webhook_message_id");
            entity.Property(e => e.EventType)
                .HasMaxLength(80)
                .HasColumnName("event_type");
            entity.Property(e => e.SignatureValid).HasColumnName("signature_valid");
            entity.Property(e => e.ProcessingStatus)
                .HasMaxLength(40)
                .HasColumnName("processing_status");
            entity.Property(e => e.ProcessingNotes).HasColumnName("processing_notes");
            entity.Property(e => e.HeadersJson)
                .HasColumnType("jsonb")
                .HasColumnName("headers_json");
            entity.Property(e => e.PayloadJson)
                .HasColumnType("jsonb")
                .HasColumnName("payload_json");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
        });

        modelBuilder.ConfigureLeadIndustryPolicies();
        modelBuilder.ConfigureLeadIntelligenceSettings();
        modelBuilder.ConfigureAiPlatformPersistence();
    }
}
