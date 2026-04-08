using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Data;

public partial class AppDbContext
{
    public virtual DbSet<CampaignChannelMetric> CampaignChannelMetrics { get; set; } = null!;
    public virtual DbSet<CampaignExecutionTask> CampaignExecutionTasks { get; set; } = null!;
}
