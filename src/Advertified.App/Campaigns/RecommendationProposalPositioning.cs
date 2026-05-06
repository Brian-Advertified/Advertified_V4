namespace Advertified.App.Campaigns;

internal static class RecommendationProposalPositioning
{
    internal static RecommendationProposalPositioningDetails Resolve(
        string? recommendationType,
        int proposalIndex,
        string? archetypeName = null)
    {
        var archetypePositioning = ResolveArchetypeProposalPlaybook(archetypeName, proposalIndex);
        if (archetypePositioning is not null)
        {
            return archetypePositioning;
        }

        var knownVariant = ResolveKnownVariant(recommendationType);
        if (knownVariant is not null)
        {
            return knownVariant;
        }

        return new RecommendationProposalPositioningDetails(
            $"Proposal {GetProposalLetter(proposalIndex)}",
            "Recommendation option");
    }

    internal static string GetKnownProposalLabel(string? recommendationType)
    {
        return ResolveKnownVariant(recommendationType)?.Label ?? string.Empty;
    }

    internal static int GetRank(string? recommendationType)
    {
        return ResolveVariantKey(recommendationType) switch
        {
            "balanced" => 0,
            "ooh_focus" => 1,
            "radio_focus" => 2,
            "digital_focus" => 2,
            "tv_focus" => 2,
            _ => 9
        };
    }

    private static RecommendationProposalPositioningDetails? ResolveKnownVariant(string? recommendationType)
    {
        return ResolveVariantKey(recommendationType) switch
        {
            "balanced" => new RecommendationProposalPositioningDetails("Proposal A", "Balanced mix"),
            "ooh_focus" => new RecommendationProposalPositioningDetails("Proposal B", "Billboards and Digital Screens-led reach"),
            "radio_focus" => new RecommendationProposalPositioningDetails("Proposal C", "Radio-led frequency"),
            "digital_focus" => new RecommendationProposalPositioningDetails("Proposal C", "Digital-led amplification"),
            "tv_focus" => new RecommendationProposalPositioningDetails("Proposal C", "TV-led reach"),
            _ => null
        };
    }

    private static RecommendationProposalPositioningDetails? ResolveArchetypeProposalPlaybook(string? archetypeName, int proposalIndex)
    {
        if (string.IsNullOrWhiteSpace(archetypeName))
        {
            return null;
        }

        var normalized = archetypeName.Trim().ToLowerInvariant();
        return normalized switch
        {
            "active scaler" => proposalIndex switch
            {
                0 => new RecommendationProposalPositioningDetails("Proposal A - Channel Extension (Test & validate)", "Add one missing high-impact channel while maintaining current digital activity. Built for low-risk validation."),
                1 => new RecommendationProposalPositioningDetails("Proposal B - Multi-Channel Expansion (Best balance)", "Capture more demand across multiple touchpoints with a balanced radio, Billboards and Digital Screens, and digital reinforcement mix."),
                _ => new RecommendationProposalPositioningDetails("Proposal C - Market Domination (Max growth)", "Maximise visibility and push category dominance locally through stronger radio, Billboards and Digital Screens, and digital amplification.")
            },
            "promo-dependent retailer" => proposalIndex switch
            {
                0 => new RecommendationProposalPositioningDetails("Proposal A - Awareness Starter (Test & validate)", "Introduce a light brand layer with small radio support and minimal digital continuity."),
                1 => new RecommendationProposalPositioningDetails("Proposal B - Promo + Brand Hybrid (Best balance)", "Turn promotions into more consistent foot traffic using radio consistency, Billboards and Digital Screens reinforcement, and digital amplification."),
                _ => new RecommendationProposalPositioningDetails("Proposal C - Always-On Presence (Max growth)", "Build continuous visibility with stronger radio, Billboards and Digital Screens presence, and ongoing digital support to reduce promo dependency.")
            },
            "invisible local business" => proposalIndex switch
            {
                0 => new RecommendationProposalPositioningDetails("Proposal A - Discovery Fix (Test & validate)", "Fix local discovery with search presence, maps optimization, and light paid support."),
                1 => new RecommendationProposalPositioningDetails("Proposal B - Local Visibility Boost (Best balance)", "Combine search and maps with radio or entry-level Billboards and Digital Screens to improve both discoverability and local awareness."),
                _ => new RecommendationProposalPositioningDetails("Proposal C - Full Local Dominance (Max growth)", "Own local visibility across search, Billboards and Digital Screens, and radio to become the most visible option in-area.")
            },
            "digital-only player" => proposalIndex switch
            {
                0 => new RecommendationProposalPositioningDetails("Proposal A - Offline Test (Test & validate)", "Run a first offline step with small radio or 1-2 Billboards and Digital Screens placements while maintaining digital performance."),
                1 => new RecommendationProposalPositioningDetails("Proposal B - Omnichannel Expansion (Best balance)", "Break through digital saturation by combining current digital strength with balanced radio and Billboards and Digital Screens."),
                _ => new RecommendationProposalPositioningDetails("Proposal C - Scale Reach Fast (Max growth)", "Expand audience reach aggressively through heavier Billboards and Digital Screens, stronger radio coverage, and digital amplification.")
            },
            "passive / untapped business" => proposalIndex switch
            {
                0 => new RecommendationProposalPositioningDetails("Proposal A - Starter Campaign (Test & validate)", "Launch a simple low-risk starter campaign with clear execution steps and BNPL support."),
                1 => new RecommendationProposalPositioningDetails("Proposal B - Growth Foundation (Best balance)", "Build a more consistent customer flow with a practical step-up in channel coverage."),
                _ => new RecommendationProposalPositioningDetails("Proposal C - Acceleration (Max growth)", "Fast-track growth with a broader multi-channel starter mix and stronger visibility pressure.")
            },
            _ => null
        };
    }

    private static string? ResolveVariantKey(string? recommendationType)
    {
        return recommendationType?
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()?
            .ToLowerInvariant();
    }

    private static string GetProposalLetter(int index)
    {
        return index >= 0 && index < 26
            ? ((char)('A' + index)).ToString()
            : (index + 1).ToString();
    }
}

internal sealed record RecommendationProposalPositioningDetails(
    string Label,
    string Strategy);
