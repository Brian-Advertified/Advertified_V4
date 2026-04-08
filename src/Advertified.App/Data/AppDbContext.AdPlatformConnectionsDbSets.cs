using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Data;

public partial class AppDbContext
{
    public virtual DbSet<AdPlatformConnection> AdPlatformConnections { get; set; } = null!;
    public virtual DbSet<CampaignAdPlatformLink> CampaignAdPlatformLinks { get; set; } = null!;
}

