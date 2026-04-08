using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Data;

public partial class AppDbContext
{
    public virtual DbSet<RecommendationRunAudit> RecommendationRunAudits { get; set; }

    public virtual DbSet<InventoryImportBatch> InventoryImportBatches { get; set; }
}
