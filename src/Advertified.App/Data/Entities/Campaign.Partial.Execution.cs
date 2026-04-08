using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class Campaign
{
    public virtual ICollection<CampaignCreativeSystem> CampaignCreativeSystems { get; set; } = new List<CampaignCreativeSystem>();

    public virtual ICollection<CampaignCreative> CampaignCreatives { get; set; } = new List<CampaignCreative>();

    public virtual ICollection<CampaignAsset> CampaignAssets { get; set; } = new List<CampaignAsset>();

    public virtual ICollection<CampaignSupplierBooking> CampaignSupplierBookings { get; set; } = new List<CampaignSupplierBooking>();

    public virtual ICollection<CampaignDeliveryReport> CampaignDeliveryReports { get; set; } = new List<CampaignDeliveryReport>();
    public virtual ICollection<CampaignChannelMetric> CampaignChannelMetrics { get; set; } = new List<CampaignChannelMetric>();
    public virtual ICollection<CampaignExecutionTask> CampaignExecutionTasks { get; set; } = new List<CampaignExecutionTask>();

    public virtual ICollection<CampaignPauseWindow> CampaignPauseWindows { get; set; } = new List<CampaignPauseWindow>();
}
