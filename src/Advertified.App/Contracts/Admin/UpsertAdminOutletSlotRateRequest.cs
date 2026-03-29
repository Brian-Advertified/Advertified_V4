namespace Advertified.App.Contracts.Admin;

public sealed class UpsertAdminOutletSlotRateRequest
{
    public string DayGroup { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int AdDurationSeconds { get; set; } = 30;
    public decimal RateZar { get; set; }
    public string RateType { get; set; } = "spot";
    public string? SourceName { get; set; }
    public DateOnly? SourceDate { get; set; }
    public bool IsActive { get; set; } = true;
}
