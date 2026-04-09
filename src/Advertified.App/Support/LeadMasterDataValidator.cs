using Advertified.App.Services.Abstractions;

namespace Advertified.App.Support;

internal static class LeadMasterDataValidator
{
    internal static Task ValidateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var masterDataService = scope.ServiceProvider.GetRequiredService<ILeadMasterDataService>();
        var tokenSet = masterDataService.GetTokenSet();

        if (tokenSet.IndustryTokens.Count == 0)
        {
            throw new InvalidOperationException("Lead master data validation failed: no industry tokens were loaded.");
        }

        if (tokenSet.LanguageTokens.Count == 0)
        {
            throw new InvalidOperationException("Lead master data validation failed: no language tokens were loaded.");
        }

        return Task.CompletedTask;
    }
}
