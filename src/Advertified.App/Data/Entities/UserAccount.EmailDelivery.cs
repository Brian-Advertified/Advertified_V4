namespace Advertified.App.Data.Entities;

public partial class UserAccount
{
    public virtual ICollection<EmailDeliveryMessage> EmailDeliveryMessages { get; set; } = new List<EmailDeliveryMessage>();
}
