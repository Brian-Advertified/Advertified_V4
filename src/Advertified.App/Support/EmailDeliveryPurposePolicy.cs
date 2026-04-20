using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

internal static class EmailDeliveryPurposePolicy
{
    public static bool HasTrackedDelivery(IEnumerable<EmailDeliveryMessage> messages, string purpose)
    {
        return messages.Any(message =>
            string.Equals(message.DeliveryPurpose, purpose, StringComparison.OrdinalIgnoreCase)
            && message.Status is not EmailDeliveryStatuses.Failed
            && message.Status is not EmailDeliveryStatuses.Bounced
            && message.Status is not EmailDeliveryStatuses.Complained);
    }
}
