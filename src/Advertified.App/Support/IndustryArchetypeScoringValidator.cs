using Advertified.App.Services.Abstractions;

namespace Advertified.App.Support;

internal static class IndustryArchetypeScoringValidator
{
    internal static Task ValidateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var scoringService = scope.ServiceProvider.GetRequiredService<IIndustryArchetypeScoringService>();
        var supportedCodes = scoringService.GetSupportedIndustryCodes();

        foreach (var requiredCode in new[]
                 {
                     LeadCanonicalValues.IndustryCodes.FuneralServices,
                     LeadCanonicalValues.IndustryCodes.Healthcare,
                     LeadCanonicalValues.IndustryCodes.LegalServices,
                     LeadCanonicalValues.IndustryCodes.Retail,
                     LeadCanonicalValues.IndustryCodes.FoodHospitality,
                     LeadCanonicalValues.IndustryCodes.Automotive
                 })
        {
            if (!supportedCodes.Contains(requiredCode, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Industry archetype scoring validation failed: missing scoring profile for '{requiredCode}'.");
            }
        }

        return Task.CompletedTask;
    }
}
