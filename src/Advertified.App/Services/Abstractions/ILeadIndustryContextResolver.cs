using Advertified.App.Services;

namespace Advertified.App.Services.Abstractions;

public interface ILeadIndustryContextResolver
{
    LeadIndustryContext ResolveFromCategory(string? category);

    IReadOnlyList<LeadIndustryContext> ResolveFromHints(IReadOnlyList<string> hints);
}
