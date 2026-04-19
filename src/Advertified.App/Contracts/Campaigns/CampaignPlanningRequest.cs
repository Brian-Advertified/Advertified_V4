namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignPlanningRequest
{
    public Guid CampaignId { get; set; }
    public decimal SelectedBudget { get; set; }
    public CampaignBusinessLocation? BusinessLocation { get; set; }
    public CampaignTargetingProfile? Targeting { get; set; }
    public PlanningBudgetAllocation? BudgetAllocation { get; set; }
    public string? Objective { get; set; }
    public string? BusinessStage { get; set; }
    public string? MonthlyRevenueBand { get; set; }
    public string? SalesModel { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? DurationWeeks { get; set; }
    public List<CampaignChannelFlightRequest> ChannelFlights { get; set; } = new();
    public string? GeographyScope { get; set; }
    public List<string> Provinces { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public List<string> Suburbs { get; set; } = new();
    public List<string> Areas { get; set; } = new();
    public string? TargetLocationLabel { get; set; }
    public string? TargetLocationCity { get; set; }
    public string? TargetLocationProvince { get; set; }
    public string? TargetLocationSource { get; set; }
    public string? TargetLocationPrecision { get; set; }
    public List<string> PreferredMediaTypes { get; set; } = new();
    public List<string> ExcludedMediaTypes { get; set; } = new();
    public List<string> TargetLanguages { get; set; } = new();
    public int? TargetAgeMin { get; set; }
    public int? TargetAgeMax { get; set; }
    public string? TargetGender { get; set; }
    public List<string> TargetInterests { get; set; } = new();
    public string? TargetAudienceNotes { get; set; }
    public string? CustomerType { get; set; }
    public string? BuyingBehaviour { get; set; }
    public string? DecisionCycle { get; set; }
    public string? PricePositioning { get; set; }
    public string? AverageCustomerSpendBand { get; set; }
    public string? GrowthTarget { get; set; }
    public string? UrgencyLevel { get; set; }
    public string? AudienceClarity { get; set; }
    public string? ValuePropositionFocus { get; set; }
    public int? TargetLsmMin { get; set; }
    public int? TargetLsmMax { get; set; }
    public List<string> MustHaveAreas { get; set; } = new();
    public List<string> ExcludedAreas { get; set; } = new();
    public bool OpenToUpsell { get; set; }
    public decimal? AdditionalBudget { get; set; }
    public int? MaxMediaItems { get; set; }
    public int? TargetRadioShare { get; set; }
    public int? TargetOohShare { get; set; }
    public int? TargetTvShare { get; set; }
    public int? TargetDigitalShare { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }

    public CampaignPlanningRequest DeepClone()
    {
        return new CampaignPlanningRequest
        {
            CampaignId = CampaignId,
            SelectedBudget = SelectedBudget,
            BusinessLocation = BusinessLocation?.DeepClone(),
            Targeting = Targeting?.DeepClone(),
            BudgetAllocation = BudgetAllocation?.DeepClone(),
            Objective = Objective,
            BusinessStage = BusinessStage,
            MonthlyRevenueBand = MonthlyRevenueBand,
            SalesModel = SalesModel,
            StartDate = StartDate,
            EndDate = EndDate,
            DurationWeeks = DurationWeeks,
            ChannelFlights = ChannelFlights
                .Select(flight => new CampaignChannelFlightRequest
                {
                    Channel = flight.Channel,
                    StartDate = flight.StartDate,
                    EndDate = flight.EndDate,
                    DurationWeeks = flight.DurationWeeks,
                    DurationMonths = flight.DurationMonths,
                    Priority = flight.Priority,
                    Notes = flight.Notes
                })
                .ToList(),
            GeographyScope = GeographyScope,
            Provinces = Provinces.ToList(),
            Cities = Cities.ToList(),
            Suburbs = Suburbs.ToList(),
            Areas = Areas.ToList(),
            TargetLocationLabel = TargetLocationLabel,
            TargetLocationCity = TargetLocationCity,
            TargetLocationProvince = TargetLocationProvince,
            TargetLocationSource = TargetLocationSource,
            TargetLocationPrecision = TargetLocationPrecision,
            PreferredMediaTypes = PreferredMediaTypes.ToList(),
            ExcludedMediaTypes = ExcludedMediaTypes.ToList(),
            TargetLanguages = TargetLanguages.ToList(),
            TargetAgeMin = TargetAgeMin,
            TargetAgeMax = TargetAgeMax,
            TargetGender = TargetGender,
            TargetInterests = TargetInterests.ToList(),
            TargetAudienceNotes = TargetAudienceNotes,
            CustomerType = CustomerType,
            BuyingBehaviour = BuyingBehaviour,
            DecisionCycle = DecisionCycle,
            PricePositioning = PricePositioning,
            AverageCustomerSpendBand = AverageCustomerSpendBand,
            GrowthTarget = GrowthTarget,
            UrgencyLevel = UrgencyLevel,
            AudienceClarity = AudienceClarity,
            ValuePropositionFocus = ValuePropositionFocus,
            TargetLsmMin = TargetLsmMin,
            TargetLsmMax = TargetLsmMax,
            MustHaveAreas = MustHaveAreas.ToList(),
            ExcludedAreas = ExcludedAreas.ToList(),
            OpenToUpsell = OpenToUpsell,
            AdditionalBudget = AdditionalBudget,
            MaxMediaItems = MaxMediaItems,
            TargetRadioShare = TargetRadioShare,
            TargetOohShare = TargetOohShare,
            TargetTvShare = TargetTvShare,
            TargetDigitalShare = TargetDigitalShare,
            TargetLatitude = TargetLatitude,
            TargetLongitude = TargetLongitude
        };
    }
}
