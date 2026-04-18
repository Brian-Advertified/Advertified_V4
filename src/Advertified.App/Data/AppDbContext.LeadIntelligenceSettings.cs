using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Data;

public partial class AppDbContext
{
    public virtual DbSet<LeadIntelligenceSetting> LeadIntelligenceSettings { get; set; } = null!;
}

internal static class AppDbContextLeadIntelligenceSettingsModelBuilderExtensions
{
    internal static void ConfigureLeadIntelligenceSettings(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadIntelligenceSetting>(entity =>
        {
            entity.HasKey(e => e.SettingKey).HasName("lead_intelligence_settings_pkey");

            entity.ToTable("lead_intelligence_settings");

            entity.Property(e => e.SettingKey)
                .HasMaxLength(120)
                .HasColumnName("setting_key");
            entity.Property(e => e.SettingValue)
                .HasColumnName("setting_value");
            entity.Property(e => e.Description)
                .HasColumnName("description");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });
    }
}
