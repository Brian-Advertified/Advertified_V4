using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Data;

public partial class AppDbContext
{
    public virtual DbSet<PackageBandProfile> PackageBandProfiles { get; set; }

    public virtual DbSet<PackageBandPreviewTier> PackageBandPreviewTiers { get; set; }

    public virtual DbSet<PackageBandAiEntitlement> PackageBandAiEntitlements { get; set; }
}
