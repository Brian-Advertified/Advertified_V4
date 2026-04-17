using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Data;

public partial class AppDbContext
{
    public virtual DbSet<EmailDeliveryProviderSetting> EmailDeliveryProviderSettings { get; set; }
    public virtual DbSet<EmailDeliveryMessage> EmailDeliveryMessages { get; set; }
    public virtual DbSet<EmailDeliveryEvent> EmailDeliveryEvents { get; set; }
    public virtual DbSet<EmailDeliveryWebhookAudit> EmailDeliveryWebhookAudits { get; set; }
}
