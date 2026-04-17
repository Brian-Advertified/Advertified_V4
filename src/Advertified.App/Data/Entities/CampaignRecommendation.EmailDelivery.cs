namespace Advertified.App.Data.Entities;

public partial class CampaignRecommendation
{
    public virtual ICollection<EmailDeliveryMessage> EmailDeliveryMessages { get; set; } = new List<EmailDeliveryMessage>();
}
