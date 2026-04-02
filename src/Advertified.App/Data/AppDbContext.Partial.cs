using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Data;

public partial class AppDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
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

            entity.Property(e => e.AssignedAgentUserId).HasColumnName("assigned_agent_user_id");
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");
            entity.Property(e => e.AssignmentEmailSentAt).HasColumnName("assignment_email_sent_at");
            entity.Property(e => e.AgentWorkStartedEmailSentAt).HasColumnName("agent_work_started_email_sent_at");
            entity.Property(e => e.RecommendationReadyEmailSentAt).HasColumnName("recommendation_ready_email_sent_at");

            entity.HasOne(e => e.AssignedAgentUser)
                .WithMany()
                .HasForeignKey(e => e.AssignedAgentUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("campaigns_assigned_agent_user_id_fkey");
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

        modelBuilder.ConfigureAiPlatformPersistence();
    }
}
