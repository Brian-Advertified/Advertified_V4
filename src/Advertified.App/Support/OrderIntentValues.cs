using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

public static class OrderIntentValues
{
    public const string Sale = "sale";
    public const string Prospect = "prospect";
}

public static class PackageOrderIntentPolicy
{
    public static bool IsProspect(PackageOrder? order)
    {
        return order is not null
            && string.Equals(order.OrderIntent, OrderIntentValues.Prospect, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSale(PackageOrder? order) => !IsProspect(order);
}
