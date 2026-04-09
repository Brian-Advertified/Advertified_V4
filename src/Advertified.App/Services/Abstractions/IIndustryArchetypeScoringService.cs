namespace Advertified.App.Services.Abstractions;

public interface IIndustryArchetypeScoringService
{
    IndustryArchetypeScoringProfile? Resolve(string? industryCode);

    IReadOnlyCollection<string> GetSupportedIndustryCodes();
}
