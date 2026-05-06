namespace Advertified.App.Campaigns;

internal static class RecommendationOpportunityContextParser
{
    public static RecommendationOpportunityContextParseResult Parse(string? rawNotes)
    {
        if (string.IsNullOrWhiteSpace(rawNotes))
        {
            return new RecommendationOpportunityContextParseResult(null, null);
        }

        var sections = rawNotes
            .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static section => !string.IsNullOrWhiteSpace(section))
            .ToList();

        var detectedGaps = new List<string>();
        string? archetypeName = null;
        string? industryProfileName = null;
        string? industryMessagingAngle = null;
        var industryGuardrails = new List<string>();
        string? industryRecommendedCta = null;
        string? whoWeAre = null;
        var researchBasis = new List<string>();
        string? lastResearchedAtUtc = null;
        string? socialQualityNote = null;
        string? leadInsightSummary = null;
        string? expectedOutcome = null;
        string? whyActNow = null;
        string? flexibleRollout = null;
        string? nextStep = null;
        var isLeadOutreach = false;
        var remainingSections = new List<string>();

        foreach (var section in sections)
        {
            if (TryParseDetectedGaps(section, detectedGaps))
            {
                isLeadOutreach = true;
                continue;
            }

            if (TryParsePrefixedSection(section, "Archetype:", out var archetype))
            {
                archetypeName = archetype;
                isLeadOutreach = true;
                continue;
            }

            if (TryParsePrefixedSection(section, "Who we are:", out var intro))
            {
                whoWeAre = intro;
                continue;
            }

            if (TryParsePrefixedSection(section, "Industry profile:", out var industryProfile))
            {
                industryProfileName = industryProfile;
                continue;
            }

            if (TryParsePrefixedSection(section, "Industry messaging angle:", out var angle))
            {
                industryMessagingAngle = angle;
                continue;
            }

            if (TryParseBulletedSection(section, "Industry guardrails:", industryGuardrails))
            {
                continue;
            }

            if (TryParsePrefixedSection(section, "Industry recommended CTA:", out var industryCta))
            {
                industryRecommendedCta = industryCta;
                continue;
            }

            if (TryParseBulletedSection(section, "Research basis:", researchBasis))
            {
                continue;
            }

            if (TryParsePrefixedSection(section, "Last researched:", out var lastResearched))
            {
                lastResearchedAtUtc = lastResearched;
                continue;
            }

            if (TryParsePrefixedSection(section, "Social quality note:", out var socialNote))
            {
                socialQualityNote = socialNote;
                continue;
            }

            if (TryParsePrefixedSection(section, "Lead intelligence summary:", out var summary))
            {
                leadInsightSummary = summary;
                continue;
            }

            if (section.StartsWith("Expected impact:", StringComparison.OrdinalIgnoreCase))
            {
                expectedOutcome = section.Trim();
                continue;
            }

            if (TryParsePrefixedSection(section, "Why act now:", out var urgency))
            {
                whyActNow = urgency;
                continue;
            }

            if (TryParsePrefixedSection(section, "Flexible rollout:", out var rollout))
            {
                flexibleRollout = rollout;
                continue;
            }

            if (TryParsePrefixedSection(section, "Next step:", out var step))
            {
                nextStep = step;
                continue;
            }

            if (TryParsePrefixedSection(section, "Outreach email draft:", out _))
            {
                // Outreach email body is CRM/operator content and should never appear inside proposal PDFs.
                continue;
            }

            if (TryParsePrefixedSection(section, "Lead source id:", out _))
            {
                // Internal provenance marker used for server-side confidence gate checks.
                continue;
            }

            remainingSections.Add(section.Trim());
        }

        var hasOpportunityContext = detectedGaps.Count > 0
            || !string.IsNullOrWhiteSpace(archetypeName)
            || !string.IsNullOrWhiteSpace(industryProfileName)
            || !string.IsNullOrWhiteSpace(industryMessagingAngle)
            || industryGuardrails.Count > 0
            || !string.IsNullOrWhiteSpace(industryRecommendedCta)
            || !string.IsNullOrWhiteSpace(whoWeAre)
            || researchBasis.Count > 0
            || !string.IsNullOrWhiteSpace(lastResearchedAtUtc)
            || !string.IsNullOrWhiteSpace(socialQualityNote)
            || !string.IsNullOrWhiteSpace(leadInsightSummary)
            || !string.IsNullOrWhiteSpace(expectedOutcome)
            || !string.IsNullOrWhiteSpace(whyActNow)
            || !string.IsNullOrWhiteSpace(flexibleRollout)
            || !string.IsNullOrWhiteSpace(nextStep);

        var campaignNotes = remainingSections.Count > 0
            ? string.Join(Environment.NewLine + Environment.NewLine, remainingSections)
            : null;

        return new RecommendationOpportunityContextParseResult(
            hasOpportunityContext
                ? new RecommendationOpportunityContextModel
                {
                    ArchetypeName = archetypeName,
                    IndustryProfileName = industryProfileName,
                    IndustryMessagingAngle = industryMessagingAngle,
                    IndustryGuardrails = industryGuardrails,
                    IndustryRecommendedCta = industryRecommendedCta,
                    WhoWeAre = whoWeAre,
                    ResearchBasis = researchBasis,
                    LastResearchedAtUtc = lastResearchedAtUtc,
                    SocialQualityNote = socialQualityNote,
                    DetectedGaps = detectedGaps,
                    LeadInsightSummary = leadInsightSummary,
                    ExpectedOutcome = expectedOutcome,
                    WhyActNow = whyActNow,
                    FlexibleRollout = flexibleRollout,
                    NextStep = nextStep,
                    IsLeadOutreach = isLeadOutreach
                }
                : null,
            campaignNotes);
    }

    private static bool TryParseDetectedGaps(string section, List<string> detectedGaps)
    {
        return TryParseBulletedSection(section, "Why you are receiving this:", detectedGaps);
    }

    private static bool TryParseBulletedSection(string section, string prefix, List<string> lines)
    {
        if (!TryParsePrefixedSection(section, prefix, out var body))
        {
            return false;
        }

        foreach (var line in body
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith("-", StringComparison.Ordinal)))
        {
            var normalized = line[1..].Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                lines.Add(normalized);
            }
        }

        return true;
    }

    private static bool TryParsePrefixedSection(string section, string prefix, out string value)
    {
        if (!section.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = string.Empty;
            return false;
        }

        value = section[prefix.Length..].Trim();
        return true;
    }
}

internal sealed record RecommendationOpportunityContextParseResult(
    RecommendationOpportunityContextModel? Context,
    string? CampaignNotes);
