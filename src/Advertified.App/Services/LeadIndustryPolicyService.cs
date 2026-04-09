using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class LeadIndustryPolicyService : ILeadIndustryPolicyService
{
    private readonly IReadOnlyList<LeadIndustryPolicyMatcher> _matchers;
    private readonly LeadIndustryPolicyProfile _defaultProfile;

    public LeadIndustryPolicyService(IOptions<LeadIndustryPolicyOptions> options)
    {
        var configuredProfiles = options.Value.Profiles;
        if (configuredProfiles.Count == 0)
        {
            configuredProfiles = LeadIndustryPolicyOptions.BuildDefaults();
        }

        var mappedProfiles = configuredProfiles
            .Select(MapProfile)
            .ToList();

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

        _matchers = configuredProfiles
            .Select((profile, index) => new LeadIndustryPolicyMatcher
            {
                Profile = mappedProfiles[index],
                Keywords = profile.MatchKeywords
                    .Select(keyword => keyword.Trim())
                    .Where(keyword => keyword.Length > 0)
                    .ToArray(),
            })
            .Where(item => !item.Profile.Key.Equals("default", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public LeadIndustryPolicyProfile ResolveForCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return _defaultProfile;
        }

        var normalizedCategory = category.Trim();
        foreach (var matcher in _matchers)
        {
            if (matcher.Keywords.Any(keyword =>
                normalizedCategory.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return matcher.Profile;
            }
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

    private sealed class LeadIndustryPolicyMatcher
    {
        public LeadIndustryPolicyProfile Profile { get; init; } = new();

        public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    }
}
