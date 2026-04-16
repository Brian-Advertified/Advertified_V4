using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class LeadIndustryPolicyService : ILeadIndustryPolicyService
{
    private readonly IReadOnlyDictionary<string, LeadIndustryPolicyProfile> _profilesByKey;
    private readonly LeadIndustryPolicyProfile _defaultProfile;
    private readonly ILeadMasterDataService _leadMasterDataService;

    public LeadIndustryPolicyService(LeadIndustryPolicySnapshotProvider snapshotProvider, ILeadMasterDataService leadMasterDataService)
    {
        _leadMasterDataService = leadMasterDataService;
        var mappedProfiles = snapshotProvider.GetCurrent();

        var distinctProfiles = mappedProfiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Key))
            .GroupBy(profile => profile.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();

        _profilesByKey = distinctProfiles
            .ToDictionary(profile => profile.Key.Trim(), StringComparer.OrdinalIgnoreCase);

        _defaultProfile = distinctProfiles.FirstOrDefault(profile =>
            profile.Key.Equals("default", StringComparison.OrdinalIgnoreCase))
            ?? distinctProfiles.FirstOrDefault()
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

    public LeadIndustryPolicyService(LeadIndustryPolicySnapshotProvider snapshotProvider)
        : this(snapshotProvider, new NoOpLeadMasterDataService())
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
    private sealed class NoOpLeadMasterDataService : ILeadMasterDataService
    {
        public LeadMasterTokenSet GetTokenSet() => new();
        public MasterLocationMatch? ResolveLocation(string? value) => null;
        public MasterIndustryMatch? ResolveIndustry(string? value) => null;
        public MasterIndustryMatch? ResolveIndustryFromHints(IReadOnlyList<string> hints) => null;
        public MasterLanguageMatch? ResolveLanguage(string? value) => null;
    }
}
