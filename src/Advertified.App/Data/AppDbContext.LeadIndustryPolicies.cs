using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Data;

public partial class AppDbContext
{
    public virtual DbSet<LeadIndustryPolicySetting> LeadIndustryPolicySettings { get; set; } = null!;
}

internal static class AppDbContextLeadIndustryPolicyModelBuilderExtensions
{
    internal static void ConfigureLeadIndustryPolicies(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadIndustryPolicySetting>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("lead_industry_policies_pkey");

            entity.ToTable("lead_industry_policies");

            entity.HasIndex(e => new { e.IsActive, e.SortOrder, e.Key }, "ix_lead_industry_policies_active_sort");

            entity.Property(e => e.Key)
                .HasMaxLength(100)
                .HasColumnName("key");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.ObjectiveOverride)
                .HasMaxLength(100)
                .HasColumnName("objective_override");
            entity.Property(e => e.PreferredTone)
                .HasMaxLength(100)
                .HasColumnName("preferred_tone");
            entity.Property(e => e.PreferredChannelsJson)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnName("preferred_channels_json");
            entity.Property(e => e.Cta).HasColumnName("cta");
            entity.Property(e => e.MessagingAngle).HasColumnName("messaging_angle");
            entity.Property(e => e.GuardrailsJson)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnName("guardrails_json");
            entity.Property(e => e.AdditionalGap).HasColumnName("additional_gap");
            entity.Property(e => e.AdditionalOutcome).HasColumnName("additional_outcome");
            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0)
                .HasColumnName("sort_order");
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
    }
}
