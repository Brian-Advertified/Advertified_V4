namespace Advertified.App.Data.Entities;

public partial class Campaign
{
    public virtual ICollection<EmailDeliveryMessage> EmailDeliveryMessages { get; set; } = new List<EmailDeliveryMessage>();
}
