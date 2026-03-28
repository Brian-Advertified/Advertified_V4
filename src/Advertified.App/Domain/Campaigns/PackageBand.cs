namespace Advertified.App.Domain.Campaigns;

public sealed class PackageBand
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MinBudget { get; set; }
    public decimal MaxBudget { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
