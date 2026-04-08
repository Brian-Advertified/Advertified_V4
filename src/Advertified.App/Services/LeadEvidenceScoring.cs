namespace Advertified.App.Services;

public static class LeadEvidenceScoring
{
    public static decimal ResolveFreshnessMultiplier(DateTime? observedAtUtc, DateTime nowUtc)
    {
        if (!observedAtUtc.HasValue)
        {
            return 0.4m;
        }

        var ageInDays = (nowUtc - observedAtUtc.Value).TotalDays;
        return ageInDays switch
        {
            <= 30 => 1.0m,
            <= 90 => 0.85m,
            <= 180 => 0.65m,
            <= 365 => 0.4m,
            _ => 0.2m
        };
    }

    public static decimal ResolveEffectiveWeight(LeadSignalEvidenceInput input, decimal freshnessMultiplier)
    {
        return Math.Round(
            input.Weight * input.ReliabilityMultiplier * freshnessMultiplier,
            2,
            MidpointRounding.AwayFromZero);
    }
}
