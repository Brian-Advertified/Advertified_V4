using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

internal static class PackageCatalogDefaults
{
    internal static IReadOnlyList<PackageBandProfileSeed> CreateSeeds(IReadOnlyCollection<PackageBand> bands)
    {
        return bands
            .Select(CreateSeed)
            .Where(x => x != null)
            .Cast<PackageBandProfileSeed>()
            .ToArray();
    }

    private static PackageBandProfileSeed? CreateSeed(PackageBand band)
    {
        var code = band.Code.Trim().ToLowerInvariant();

        return code switch
        {
            "launch" => new PackageBandProfileSeed(
                band.Id,
                "Best for first campaigns that need a crisp local footprint and a guided rollout.",
                "Growing SMEs entering a market or announcing a new offer.",
                "Local visibility for smaller campaign starts.",
                "Best for first-time advertisers, local campaigns, and single-area visibility.",
                "optional",
                "no",
                "5 business days",
                35000m,
                false,
                new[] { "Curated local media mix", "Brief-to-launch guidance", "AI planning unlock after payment" },
                CreateTiers(
                    ("entry", "Entry Launch mix", new[] { "1-2 local media items", "Focused on one target area", "Starter outdoor, digital, or radio route" }, new[] { "Usually one area", "Lighter frequency", "Simple local visibility" }),
                    ("mid", "Balanced Launch mix", new[] { "1-2 stronger local placements", "Starter mixed-media possibility", "More concentrated visibility in one zone" }, new[] { "Better local frequency", "Stronger promotion support", "Tighter awareness burst" }),
                    ("premium", "Expanded Launch mix", new[] { "2 local media items", "Stronger starter mix", "Higher visibility within one main area" }, new[] { "Higher local presence", "More sustained exposure", "Better support for launches and offers" }))),
            "boost" => new PackageBandProfileSeed(
                band.Id,
                "Built for brands that need stronger frequency, reach, and clear commercial momentum.",
                "Businesses ready to push awareness, leads, or foot traffic at regional level.",
                "More reach and frequency across selected areas.",
                "Best for growing brands needing more reach, repetition, and selected multi-area visibility.",
                "yes",
                "yes",
                "7 business days",
                75000m,
                true,
                new[] { "Regional reach planning", "Structured upsell options", "Client dashboard progress tracking" },
                CreateTiers(
                    ("entry", "Entry Boost mix", new[] { "2-3 media items", "Stronger outdoor footprint", "Potential support in selected markets" }, new[] { "Local-to-regional coverage", "More frequency than Launch", "Better repeat exposure" }),
                    ("mid", "Balanced Boost mix", new[] { "2-4 media items", "Outdoor plus radio support", "Coverage across more than one area" }, new[] { "Regional promotion support", "Improved audience reach", "Stronger repeat presence" }),
                    ("premium", "Expanded Boost mix", new[] { "3-4 media items", "Broader outdoor footprint", "More confident multi-area support" }, new[] { "Higher regional visibility", "Stronger frequency", "Broader market coverage" }))),
            "scale" => new PackageBandProfileSeed(
                band.Id,
                "For serious campaign pushes that combine awareness with broader media coordination.",
                "Established brands ready to expand into larger catchments.",
                "Regional campaign weight with a stronger media mix.",
                "Best for established brands running stronger regional campaigns with multi-channel intent.",
                "yes",
                "yes",
                "10 business days",
                250000m,
                false,
                new[] { "Broader media mix potential", "Hybrid planning support", "More detailed recommendation review" },
                CreateTiers(
                    ("entry", "Entry Scale mix", new[] { "3-4 media items", "Multi-channel direction", "Stronger radio and outdoor balance" }, new[] { "Wider regional coverage", "Higher frequency", "More refined targeting" }),
                    ("mid", "Balanced Scale mix", new[] { "4-5 media items", "More tailored media mix", "Broader reach across target zones" }, new[] { "Stronger sustained presence", "Improved market coverage", "Better audience fit" }),
                    ("premium", "Expanded Scale mix", new[] { "5-6 media items", "Stronger multi-channel plan", "Broader regional support" }, new[] { "Wider and more frequent exposure", "Better premium visibility", "More tailored optimisation potential" }))),
            "dominance" => new PackageBandProfileSeed(
                band.Id,
                "A premium path for large campaigns that need strategic orchestration and executive confidence.",
                "Market leaders launching major campaigns across multiple channels.",
                "Premium visibility with broader market coverage.",
                "Best for large campaigns needing wide reach, premium visibility, and stronger strategic handling.",
                "yes",
                "yes",
                "14 business days",
                900000m,
                false,
                new[] { "Priority planning support", "Agent-led optimisation", "Executive-ready recommendation packs" },
                CreateTiers(
                    ("entry", "Entry Dominance mix", new[] { "3-4 media items", "Premium outdoor plus radio direction", "Strong regional presence" }, new[] { "Broader regional coverage", "Higher-frequency exposure", "Stronger premium visibility" }),
                    ("mid", "Balanced Dominance mix", new[] { "4-6 media items", "Broader premium mix", "More frequency and stronger multi-region support" }, new[] { "Wider regional footprint", "More premium dayparts and placements", "Stronger sustained awareness" }),
                    ("premium", "National-scale Dominance mix", new[] { "5-7 media items", "Premium multi-channel plan", "Broad market or national-scale options" }, new[] { "Higher visibility across more regions", "More advanced premium mix", "Stronger executive campaign presence" }))),
            _ => null
        };
    }

    private static IReadOnlyList<PackageBandPreviewTierSeed> CreateTiers(params (string TierCode, string TierLabel, string[] TypicalInclusions, string[] IndicativeMix)[] tiers)
    {
        return tiers
            .Select(tier => new PackageBandPreviewTierSeed(
                tier.TierCode,
                tier.TierLabel,
                tier.TypicalInclusions,
                tier.IndicativeMix))
            .ToArray();
    }
}

internal sealed record PackageBandProfileSeed(
    Guid PackageBandId,
    string Description,
    string AudienceFit,
    string QuickBenefit,
    string PackagePurpose,
    string IncludeRadio,
    string IncludeTv,
    string LeadTimeLabel,
    decimal RecommendedSpend,
    bool IsRecommended,
    IReadOnlyList<string> Benefits,
    IReadOnlyList<PackageBandPreviewTierSeed> Tiers);

internal sealed record PackageBandPreviewTierSeed(
    string TierCode,
    string TierLabel,
    IReadOnlyList<string> TypicalInclusions,
    IReadOnlyList<string> IndicativeMix);
