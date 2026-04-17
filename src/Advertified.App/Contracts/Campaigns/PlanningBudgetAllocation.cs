namespace Advertified.App.Contracts.Campaigns;

public sealed class PlanningBudgetAllocation
{
    public string ChannelPolicyKey { get; set; } = string.Empty;
    public string GeoPolicyKey { get; set; } = string.Empty;
    public string AudienceSegment { get; set; } = string.Empty;
    public List<PlanningChannelAllocation> ChannelAllocations { get; set; } = new();
    public List<PlanningGeoAllocation> GeoAllocations { get; set; } = new();
    public List<PlanningAllocationLine> CompositeAllocations { get; set; } = new();

    public PlanningBudgetAllocation DeepClone()
    {
        return new PlanningBudgetAllocation
        {
            ChannelPolicyKey = ChannelPolicyKey,
            GeoPolicyKey = GeoPolicyKey,
            AudienceSegment = AudienceSegment,
            ChannelAllocations = ChannelAllocations.Select(static allocation => allocation.DeepClone()).ToList(),
            GeoAllocations = GeoAllocations.Select(static allocation => allocation.DeepClone()).ToList(),
            CompositeAllocations = CompositeAllocations.Select(static allocation => allocation.DeepClone()).ToList()
        };
    }
}

public sealed class PlanningChannelAllocation
{
    public string Channel { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Amount { get; set; }

    public PlanningChannelAllocation DeepClone()
    {
        return new PlanningChannelAllocation
        {
            Channel = Channel,
            Weight = Weight,
            Amount = Amount
        };
    }
}

public sealed class PlanningGeoAllocation
{
    public string Bucket { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Amount { get; set; }
    public double? RadiusKm { get; set; }

    public PlanningGeoAllocation DeepClone()
    {
        return new PlanningGeoAllocation
        {
            Bucket = Bucket,
            Weight = Weight,
            Amount = Amount,
            RadiusKm = RadiusKm
        };
    }
}

public sealed class PlanningAllocationLine
{
    public string Channel { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Amount { get; set; }
    public double? RadiusKm { get; set; }

    public PlanningAllocationLine DeepClone()
    {
        return new PlanningAllocationLine
        {
            Channel = Channel,
            Bucket = Bucket,
            Weight = Weight,
            Amount = Amount,
            RadiusKm = RadiusKm
        };
    }
}
