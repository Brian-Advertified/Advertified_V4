using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class LeadIndustryPolicyService : ILeadIndustryPolicyService
{
    private readonly IReadOnlyDictionary<string, LeadIndustryPolicyProfile> _profilesByKey;
    private readonly LeadIndustryPolicyProfile _defaultProfile;
    private readonly ILeadMasterDataService _leadMasterDataService;

    public LeadIndustryPolicyService(IOptions<LeadIndustryPolicyOptions> options, ILeadMasterDataService leadMasterDataService)
    {
        _leadMasterDataService = leadMasterDataService;
        var configuredProfiles = options.Value.Profiles;
        if (configuredProfiles.Count == 0)
        {
            configuredProfiles = LeadIndustryPolicyOptions.BuildDefaults();
        }

        var mappedProfiles = configuredProfiles
            .Select(MapProfile)
            .ToList();

        _profilesByKey = mappedProfiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Key))
            .ToDictionary(profile => profile.Key.Trim(), StringComparer.OrdinalIgnoreCase);

        _defaultProfile = mappedProfiles.FirstOrDefault(profile =>
            profile.Key.Equals("default", StringComparison.OrdinalIgnoreCase))
            ?? mappedProfiles.FirstOrDefault()
            ?? new LeadIndustryPolicyProfile
            {
                Key = "default",
                Name = "General Services",
                PreferredChannels = new[] { "Digital", "OOH" },
                Cta = "Contact us to get started",
                MessagingAngle = "visible, credible, and easy to act on",
                Guardrails = new[]
                {
                    "Keep claims realistic and evidence-based.",
                    "Use clear, practical CTA language.",
                    "Align messaging with local demand intent.",
                },
                AdditionalGap = "Opportunity to tighten channel mix around the strongest local demand signals.",
                AdditionalOutcome = "Expected impact: clearer positioning and better conversion from existing demand.",
            };
    }

    public LeadIndustryPolicyService(IOptions<LeadIndustryPolicyOptions> options)
        : this(options, new NoOpLeadMasterDataService())
    {
    }

    public LeadIndustryPolicyProfile ResolveForCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return _defaultProfile;
        }

        var industryCode = _leadMasterDataService.ResolveIndustry(category)?.Code;
        if (!string.IsNullOrWhiteSpace(industryCode)
            && _profilesByKey.TryGetValue(industryCode, out var canonicalProfile))
        {
            return canonicalProfile;
        }

        var normalizedCategory = category.Trim();
        if (_profilesByKey.TryGetValue(normalizedCategory, out var directProfile))
        {
            return directProfile;
        }

        return _defaultProfile;
    }

    private static LeadIndustryPolicyProfile MapProfile(LeadIndustryPolicyProfileOptions source)
    {
        return new LeadIndustryPolicyProfile
        {
            Key = source.Key,
            Name = source.Name,
            ObjectiveOverride = source.ObjectiveOverride,
            PreferredTone = source.PreferredTone,
            PreferredChannels = source.PreferredChannels
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Cta = source.Cta,
            MessagingAngle = source.MessagingAngle,
            Guardrails = source.Guardrails
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray(),
            AdditionalGap = source.AdditionalGap,
            AdditionalOutcome = source.AdditionalOutcome,
        };
    }

    private sealed class NoOpLeadMasterDataService : ILeadMasterDataService
    {
        public LeadMasterTokenSet GetTokenSet() => new();
        public MasterLocationMatch? ResolveLocation(string? value) => null;
        public MasterIndustryMatch? ResolveIndustry(string? value) => null;
        public MasterIndustryMatch? ResolveIndustryFromHints(IReadOnlyList<string> hints) => null;
        public MasterLanguageMatch? ResolveLanguage(string? value) => null;
    }
}
