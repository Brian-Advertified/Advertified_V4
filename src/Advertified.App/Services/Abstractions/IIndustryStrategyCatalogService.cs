namespace Advertified.App.Services.Abstractions;

public interface IIndustryStrategyCatalogService
{
    IndustryStrategyCatalogProfile? Resolve(string? industryCode);

    IReadOnlyCollection<string> GetSupportedIndustryCodes();
}
