using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class LeadIndustryContextResolver : ILeadIndustryContextResolver
{
    private readonly ILeadMasterDataService _leadMasterDataService;
    private readonly ILeadIndustryPolicyService _leadIndustryPolicyService;
    private readonly IIndustryArchetypeScoringService _industryArchetypeScoringService;
    private readonly IIndustryStrategyCatalogService _industryStrategyCatalogService;

    public LeadIndustryContextResolver(
        ILeadMasterDataService leadMasterDataService,
        ILeadIndustryPolicyService leadIndustryPolicyService,
        IIndustryArchetypeScoringService industryArchetypeScoringService,
        IIndustryStrategyCatalogService industryStrategyCatalogService)
    {
        _leadMasterDataService = leadMasterDataService;
        _leadIndustryPolicyService = leadIndustryPolicyService;
        _industryArchetypeScoringService = industryArchetypeScoringService;
        _industryStrategyCatalogService = industryStrategyCatalogService;
    }

    public LeadIndustryContext ResolveFromCategory(string? category)
    {
        var canonicalIndustry = _leadMasterDataService.ResolveIndustry(category);
        return BuildContext(category, canonicalIndustry);
    }

    public IReadOnlyList<LeadIndustryContext> ResolveFromHints(IReadOnlyList<string> hints)
    {
        if (hints.Count == 0)
        {
            return Array.Empty<LeadIndustryContext>();
        }

        var contexts = new List<LeadIndustryContext>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hint in hints)
        {
            if (string.IsNullOrWhiteSpace(hint))
            {
                continue;
            }

            var canonicalIndustry = _leadMasterDataService.ResolveIndustryFromHints(new[] { hint });
            if (canonicalIndustry is not null)
            {
                if (seenCodes.Add(canonicalIndustry.Code))
                {
                    contexts.Add(BuildContext(hint, canonicalIndustry));
                }

                continue;
            }

            var fallbackCode = ResolveFallbackIndustryCode(hint);
            if (string.IsNullOrWhiteSpace(fallbackCode) || !seenCodes.Add(fallbackCode))
            {
                continue;
            }

            contexts.Add(BuildContext(
                hint,
                new MasterIndustryMatch
                {
                    Code = fallbackCode,
                    Label = ToDisplayLabel(fallbackCode)
                }));
        }

        return contexts;
    }

    private LeadIndustryContext BuildContext(string? sourceValue, MasterIndustryMatch? canonicalIndustry)
    {
        var policy = _leadIndustryPolicyService.ResolveForCategory(sourceValue, canonicalIndustry);
        var archetypeScoringProfile = _industryArchetypeScoringService.Resolve(canonicalIndustry?.Code);
        var strategyProfile = _industryStrategyCatalogService.Resolve(canonicalIndustry?.Code);
        var preferredChannels = (policy.PreferredChannels.Count > 0
                ? policy.PreferredChannels
                : strategyProfile?.Channels.PreferredChannels ?? Array.Empty<string>())
            .Where(static channel => !string.IsNullOrWhiteSpace(channel))
            .Select(static channel => channel.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LeadIndustryContext
        {
            CanonicalIndustry = canonicalIndustry,
            Policy = policy,
            ArchetypeScoringProfile = archetypeScoringProfile,
            Audience = BuildAudienceProfile(canonicalIndustry, archetypeScoringProfile, strategyProfile),
            Campaign = BuildCampaignProfile(policy, strategyProfile),
            Channels = BuildChannelProfile(preferredChannels, strategyProfile),
            Creative = BuildCreativeProfile(policy, strategyProfile),
            Compliance = BuildComplianceProfile(policy, canonicalIndustry?.Code, strategyProfile),
            Planning = new IndustryPlanningProfile
            {
                ArchetypeCode = archetypeScoringProfile?.IndustryCode ?? canonicalIndustry?.Code ?? string.Empty,
                MetadataTagMatchScore = archetypeScoringProfile?.MetadataTagMatchScore ?? 0m,
                MediaTypeScores = archetypeScoringProfile?.MediaTypeScores
                    ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
                AudienceHintScores = archetypeScoringProfile?.AudienceHintScores
                    ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            },
            Research = strategyProfile?.Research ?? new IndustryResearchProfile()
        };
    }

    private static IndustryAudienceProfile BuildAudienceProfile(
        MasterIndustryMatch? canonicalIndustry,
        IndustryArchetypeScoringProfile? archetypeScoringProfile,
        IndustryStrategyCatalogProfile? strategyProfile)
    {
        if (strategyProfile is not null)
        {
            return new IndustryAudienceProfile
            {
                PrimaryPersona = strategyProfile.Audience.PrimaryPersona,
                BuyingJourney = strategyProfile.Audience.BuyingJourney,
                TrustSensitivity = strategyProfile.Audience.TrustSensitivity,
                DefaultLanguageBiases = strategyProfile.Audience.DefaultLanguageBiases,
                AudienceHints = archetypeScoringProfile?.AudienceHintScores.Keys.ToArray() ?? Array.Empty<string>()
            };
        }

        var code = canonicalIndustry?.Code;
        return code switch
        {
            LeadCanonicalValues.IndustryCodes.FuneralServices => new IndustryAudienceProfile
            {
                PrimaryPersona = "Family decision-makers",
                BuyingJourney = "urgent, trust-led",
                TrustSensitivity = "high",
                DefaultLanguageBiases = new[] { "English" },
                AudienceHints = archetypeScoringProfile?.AudienceHintScores.Keys.ToArray() ?? Array.Empty<string>()
            },
            LeadCanonicalValues.IndustryCodes.Healthcare => new IndustryAudienceProfile
            {
                PrimaryPersona = "Nearby patients seeking care",
                BuyingJourney = "high-intent local search",
                TrustSensitivity = "high",
                DefaultLanguageBiases = new[] { "English" },
                AudienceHints = archetypeScoringProfile?.AudienceHintScores.Keys.ToArray() ?? Array.Empty<string>()
            },
            LeadCanonicalValues.IndustryCodes.Retail => new IndustryAudienceProfile
            {
                PrimaryPersona = "Price-sensitive local shoppers",
                BuyingJourney = "promotion-led repeat purchase",
                TrustSensitivity = "medium",
                DefaultLanguageBiases = new[] { "English" },
                AudienceHints = archetypeScoringProfile?.AudienceHintScores.Keys.ToArray() ?? Array.Empty<string>()
            },
            _ => new IndustryAudienceProfile
            {
                PrimaryPersona = "Growth-focused local buyers",
                BuyingJourney = "considered local demand capture",
                TrustSensitivity = "medium",
                DefaultLanguageBiases = new[] { "English" },
                AudienceHints = archetypeScoringProfile?.AudienceHintScores.Keys.ToArray() ?? Array.Empty<string>()
            }
        };
    }

    private static IndustryCampaignProfile BuildCampaignProfile(
        LeadIndustryPolicyProfile policy,
        IndustryStrategyCatalogProfile? strategyProfile)
    {
        var strategyCampaign = strategyProfile?.Campaign;
        var defaultObjective = policy.ObjectiveOverride
            ?? strategyCampaign?.DefaultObjective
            ?? "awareness";

        return new IndustryCampaignProfile
        {
            DefaultObjective = defaultObjective,
            FunnelShape = strategyCampaign?.FunnelShape
                ?? (defaultObjective.Equals("leads", StringComparison.OrdinalIgnoreCase)
                ? "conversion-led"
                : defaultObjective.Equals("promotion", StringComparison.OrdinalIgnoreCase)
                    ? "promotion-led"
                    : "balanced"),
            PrimaryKpis = strategyCampaign?.PrimaryKpis.Count > 0
                ? strategyCampaign.PrimaryKpis
                : BuildPrimaryKpis(defaultObjective),
            SalesCycle = strategyCampaign?.SalesCycle
                ?? (defaultObjective.Equals("leads", StringComparison.OrdinalIgnoreCase)
                ? "short-response"
                : "mixed")
        };
    }

    private static IndustryChannelProfile BuildChannelProfile(
        IReadOnlyList<string> preferredChannels,
        IndustryStrategyCatalogProfile? strategyProfile)
    {
        return new IndustryChannelProfile
        {
            PreferredChannels = preferredChannels,
            BaseBudgetSplit = strategyProfile?.Channels.BaseBudgetSplit.Count > 0
                ? strategyProfile.Channels.BaseBudgetSplit
                : BuildBaseBudgetSplit(preferredChannels),
            GeographyBias = !string.IsNullOrWhiteSpace(strategyProfile?.Channels.GeographyBias)
                ? strategyProfile.Channels.GeographyBias
                : (preferredChannels.Any(channel =>
                channel.Equals("OOH", StringComparison.OrdinalIgnoreCase)
                || channel.Equals("Radio", StringComparison.OrdinalIgnoreCase))
                ? "local-first"
                : "balanced")
        };
    }

    private static IndustryCreativeProfile BuildCreativeProfile(
        LeadIndustryPolicyProfile policy,
        IndustryStrategyCatalogProfile? strategyProfile)
    {
        var strategyCreative = strategyProfile?.Creative;
        return new IndustryCreativeProfile
        {
            PreferredTone = policy.PreferredTone
                ?? strategyCreative?.PreferredTone
                ?? string.Empty,
            MessagingAngle = !string.IsNullOrWhiteSpace(policy.MessagingAngle)
                ? policy.MessagingAngle
                : strategyCreative?.MessagingAngle ?? string.Empty,
            RecommendedCta = !string.IsNullOrWhiteSpace(policy.Cta)
                ? policy.Cta
                : strategyCreative?.RecommendedCta ?? string.Empty,
            ProofPoints = BuildProofPoints(policy, strategyProfile)
        };
    }

    private static IndustryComplianceProfile BuildComplianceProfile(
        LeadIndustryPolicyProfile policy,
        string? canonicalIndustryCode,
        IndustryStrategyCatalogProfile? strategyProfile)
    {
        var strategyCompliance = strategyProfile?.Compliance;
        var mergedGuardrails = (strategyCompliance?.Guardrails ?? Array.Empty<string>())
            .Concat(policy.Guardrails)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new IndustryComplianceProfile
        {
            Guardrails = mergedGuardrails,
            RestrictedClaimTypes = strategyCompliance?.RestrictedClaimTypes.Count > 0
                ? strategyCompliance.RestrictedClaimTypes
                : BuildRestrictedClaimTypes(canonicalIndustryCode)
        };
    }

    private static IReadOnlyList<string> BuildProofPoints(
        LeadIndustryPolicyProfile policy,
        IndustryStrategyCatalogProfile? strategyProfile)
    {
        return (strategyProfile?.Creative.ProofPoints ?? Array.Empty<string>())
            .Concat(new[]
            {
                policy.MessagingAngle,
                policy.AdditionalOutcome
            })
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildRestrictedClaimTypes(string? canonicalIndustryCode)
    {
        return canonicalIndustryCode switch
        {
            LeadCanonicalValues.IndustryCodes.Healthcare => new[] { "treatment_guarantees", "absolute_outcomes" },
            LeadCanonicalValues.IndustryCodes.LegalServices => new[] { "guaranteed_case_outcomes", "misleading_certainty" },
            LeadCanonicalValues.IndustryCodes.FuneralServices => new[] { "aggressive_urgency", "discount_pressure" },
            _ => Array.Empty<string>()
        };
    }

    private static IReadOnlyList<string> BuildPrimaryKpis(string? objectiveOverride)
    {
        return objectiveOverride?.Trim().ToLowerInvariant() switch
        {
            "leads" => new[] { "qualified enquiries", "conversion rate", "response time" },
            "promotion" => new[] { "footfall", "offer response", "repeat visits" },
            "foot_traffic" => new[] { "footfall", "directions", "visit intent" },
            "brand_presence" => new[] { "reach", "share of voice", "branded demand" },
            _ => new[] { "reach", "engagement", "lead flow" }
        };
    }

    private static IReadOnlyDictionary<string, int> BuildBaseBudgetSplit(IReadOnlyList<string> preferredChannels)
    {
        var normalizedChannels = preferredChannels
            .Select(NormalizeChannel)
            .Where(static channel => channel.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        if (normalizedChannels.Length == 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Digital"] = 50,
                ["OOH"] = 30,
                ["Radio"] = 20
            };
        }

        var split = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Digital"] = 0,
            ["OOH"] = 0,
            ["Radio"] = 0
        };

        var weights = normalizedChannels.Length switch
        {
            1 => new[] { 100 },
            2 => new[] { 60, 40 },
            _ => new[] { 50, 30, 20 }
        };

        for (var index = 0; index < normalizedChannels.Length; index++)
        {
            split[normalizedChannels[index]] = weights[index];
        }

        return split;
    }

    private static string? ResolveFallbackIndustryCode(string hint)
    {
        var normalizedHint = Normalize(hint);
        if (ContainsAny(normalizedHint, "automotive", "dealership", "vehicle", "car", "motor"))
        {
            return LeadCanonicalValues.IndustryCodes.Automotive;
        }

        if (ContainsAny(normalizedHint, "restaurant", "food", "takeaway", "cafe", "diner", "pizza"))
        {
            return LeadCanonicalValues.IndustryCodes.FoodHospitality;
        }

        return null;
    }

    private static bool ContainsAny(string normalizedSource, params string[] aliases)
    {
        return aliases.Any(alias => normalizedSource.Contains(Normalize(alias), StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeChannel(string channel)
    {
        return Normalize(channel) switch
        {
            "search" or "social" or "display" or "digital" => "Digital",
            "ooh" or "billboards_ooh" => "OOH",
            "radio" => "Radio",
            _ => string.Empty
        };
    }

    private static string ToDisplayLabel(string code)
    {
        var parts = code
            .Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant());
        return string.Join(" ", parts);
    }
}
