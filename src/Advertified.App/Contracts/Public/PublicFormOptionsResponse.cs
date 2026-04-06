namespace Advertified.App.Contracts.Public;

public sealed class PublicFormOptionsResponse
{
    public IReadOnlyList<FormOptionResponse> BusinessTypes { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> Industries { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> Provinces { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> RevenueBands { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> BusinessStages { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> MonthlyRevenueBands { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> SalesModels { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> CustomerTypes { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> BuyingBehaviours { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> DecisionCycles { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> GrowthTargets { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> PricePositioning { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> AverageCustomerSpendBands { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> UrgencyLevels { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> AudienceClarity { get; init; } = Array.Empty<FormOptionResponse>();
    public IReadOnlyList<FormOptionResponse> ValuePropositionFocus { get; init; } = Array.Empty<FormOptionResponse>();
}

public sealed class FormOptionResponse
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}
