using Advertified.App.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

internal sealed class RecommendationProposalIntelligenceService : IRecommendationProposalIntelligenceService
{
    public RecommendationProposalIntelligenceResult Build(RecommendationProposalIntelligenceRequest request)
    {
        var positioning = RecommendationProposalPositioning.Resolve(
            request.Recommendation.RecommendationType,
            request.ProposalIndex,
            request.OpportunityContext?.ArchetypeName);
        var mediaItems = request.Items
            .Where(IsMediaItem)
            .ToArray();
        var clientChallenge = BuildClientChallenge(request, mediaItems);
        var strategicApproach = BuildStrategicApproach(request, mediaItems);
        var expectedOutcome = BuildExpectedOutcome(request, mediaItems);
        var channelRoles = BuildChannelRoles(request, mediaItems);
        var successMeasures = BuildSuccessMeasures(mediaItems);
        var summary = BuildSummary(request, positioning, mediaItems);
        var rationale = BuildRationale(request, strategicApproach, clientChallenge);

        return new RecommendationProposalIntelligenceResult(
            positioning.Label,
            positioning.Strategy,
            summary,
            rationale,
            new RecommendationProposalNarrativeDocumentModel
            {
                ClientChallenge = clientChallenge,
                StrategicApproach = strategicApproach,
                ExpectedOutcome = expectedOutcome,
                ChannelRoles = channelRoles,
                SuccessMeasures = successMeasures
            });
    }

    private static bool IsMediaItem(RecommendationLineDocumentModel item)
    {
        return !string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSummary(
        RecommendationProposalIntelligenceRequest request,
        RecommendationProposalPositioningDetails positioning,
        IReadOnlyList<RecommendationLineDocumentModel> mediaItems)
    {
        var suppliedSummary = RecommendationPdfCopy.ToClientCopy(request.Recommendation.Summary);
        if (!string.IsNullOrWhiteSpace(suppliedSummary) && !LooksLikePlannerSummary(suppliedSummary))
        {
            return suppliedSummary;
        }

        var business = ResolveBusinessReference(request.Campaign);
        if (mediaItems.Count == 0)
        {
            return $"Proposal details are being prepared for {business}.";
        }

        var channels = ResolveChannelPhrase(mediaItems);
        var area = ResolveAreaPhrase(request.Campaign, mediaItems);
        var objective = RecommendationPdfCopy.ToClientCopy(request.Campaign.CampaignObjective);
        var channelAction = CountDistinctChannels(mediaItems) > 1 ? "pairs" : "uses";

        if (!string.IsNullOrWhiteSpace(objective))
        {
            return $"{positioning.Strategy} for {business}: the plan {channelAction} {channels} across {area} to turn the campaign objective into clearer visibility, recall, and response: {objective}.";
        }

        return $"{positioning.Strategy} for {business}: the plan {channelAction} {channels} across {area} to create stronger market presence and measurable response.";
    }

    private static string BuildRationale(
        RecommendationProposalIntelligenceRequest request,
        string strategicApproach,
        string clientChallenge)
    {
        var suppliedRationale = RecommendationRationaleSupport.RemoveInternalMarkers(request.Recommendation.Rationale);
        suppliedRationale = RecommendationPdfCopy.ToClientCopy(suppliedRationale);
        if (!string.IsNullOrWhiteSpace(suppliedRationale) && !LooksLikePlannerRationale(suppliedRationale))
        {
            return suppliedRationale;
        }

        return $"{clientChallenge} {strategicApproach}";
    }

    private static string BuildClientChallenge(
        RecommendationProposalIntelligenceRequest request,
        IReadOnlyList<RecommendationLineDocumentModel> mediaItems)
    {
        var business = ResolveBusinessReference(request.Campaign);
        var gaps = request.OpportunityContext?.DetectedGaps
            .Where(static gap => !string.IsNullOrWhiteSpace(gap))
            .Select(RecommendationPdfCopy.ToClientCopy)
            .Take(2)
            .ToArray()
            ?? Array.Empty<string>();
        if (gaps.Length > 0)
        {
            return $"The immediate challenge for {business}: {FormatList(gaps)}. This route focuses on turning those gaps into clearer visibility, stronger recall, and easier customer response.";
        }

        var objective = RecommendationPdfCopy.ToClientCopy(request.Campaign.CampaignObjective);
        var area = ResolveAreaPhrase(request.Campaign, mediaItems);
        if (!string.IsNullOrWhiteSpace(objective))
        {
            return $"{business} needs the campaign objective to show up in-market as repeated visibility and easier response across {area}: {objective}.";
        }

        return $"{business} needs a campaign route that creates visible, repeated presence across {area} while keeping the message simple enough to remember and act on.";
    }

    private static string BuildStrategicApproach(
        RecommendationProposalIntelligenceRequest request,
        IReadOnlyList<RecommendationLineDocumentModel> mediaItems)
    {
        if (mediaItems.Count == 0)
        {
            return "The plan stays in preparation until confirmed media placements are available to review.";
        }

        var primaryChannel = ResolvePrimaryChannel(mediaItems);
        var supportChannels = mediaItems
            .Select(item => NormalizeChannel(item.Channel))
            .Where(channel => !string.Equals(channel, primaryChannel, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(ResolveChannelLabel)
            .Take(3)
            .ToArray();
        var primaryLabel = ResolveChannelLabel(primaryChannel);
        var area = ResolveAreaPhrase(request.Campaign, mediaItems);

        if (supportChannels.Length == 0)
        {
            return $"The plan concentrates the campaign through {primaryLabel} to create a clear, repeatable presence across {area}.";
        }

        var hasOffline = mediaItems
            .Select(item => NormalizeChannel(item.Channel))
            .Any(channel => channel is "ooh" or "radio" or "tv" or "newspaper");
        var hasDigital = mediaItems
            .Select(item => NormalizeChannel(item.Channel))
            .Any(channel => string.Equals(channel, "digital", StringComparison.OrdinalIgnoreCase));
        if (hasOffline && hasDigital)
        {
            return $"The plan pairs reach media with response media under one campaign route: the {primaryLabel} layer creates market presence, while supporting {FormatList(supportChannels)} keeps attention active and captures response across {area}.";
        }

        return $"The plan leads with {primaryLabel} and uses {FormatList(supportChannels)} to keep the campaign visible in more than one customer moment across {area}.";
    }

    private static string BuildExpectedOutcome(
        RecommendationProposalIntelligenceRequest request,
        IReadOnlyList<RecommendationLineDocumentModel> mediaItems)
    {
        if (!string.IsNullOrWhiteSpace(request.OpportunityContext?.ExpectedOutcome))
        {
            return RecommendationPdfCopy.ToClientCopy(
                StripPrefix(request.OpportunityContext.ExpectedOutcome, "Expected impact:"));
        }

        var channels = mediaItems
            .Select(item => NormalizeChannel(item.Channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasOffline = channels.Any(channel => channel is "ooh" or "radio" or "tv" or "newspaper");
        var hasDigital = channels.Any(channel => string.Equals(channel, "digital", StringComparison.OrdinalIgnoreCase));

        if (hasOffline && hasDigital)
        {
            return "A stronger market presence that combines offline visibility with online demand capture, giving the team clearer signals from enquiries, calls, visits, and campaign engagement.";
        }

        if (hasOffline)
        {
            return "A stronger local presence and better message recall in the selected market, with campaign response tracked through enquiries, calls, visits, and sales notes.";
        }

        if (hasDigital)
        {
            return "A clearer online demand-capture path, with performance tracked through clicks, calls, forms, store visits, and other conversion signals available to the campaign.";
        }

        return "A cleaner campaign route with measurable delivery checks and post-campaign response signals against the original brief.";
    }

    private static IReadOnlyList<string> BuildChannelRoles(
        RecommendationProposalIntelligenceRequest request,
        IReadOnlyList<RecommendationLineDocumentModel> mediaItems)
    {
        return mediaItems
            .GroupBy(item => NormalizeChannel(item.Channel), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => GetChannelSortOrder(group.Key))
            .Select(group => BuildChannelRole(request.Campaign, group.Key, group.ToArray()))
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    private static string BuildChannelRole(
        RecommendationProposalCampaignContext campaign,
        string channel,
        IReadOnlyList<RecommendationLineDocumentModel> items)
    {
        var area = ResolveAreaPhrase(campaign, items);
        var label = ResolveChannelLabel(channel);

        return channel switch
        {
            "ooh" => $"{label}: builds repeated commuter and shopper visibility around {area}, keeping the brand physically present near movement and purchase zones.",
            "radio" => $"{label}: keeps the campaign present during daily listening routines, helping the message move from recognition to recall.",
            "digital" => $"{label}: reinforces attention after offline exposure and converts interest into measurable engagement.",
            "tv" => $"{label}: adds broad awareness and credibility when the market needs to recognise the brand quickly.",
            "newspaper" => $"{label}: adds credibility in a trusted local reading environment for audiences making more considered decisions.",
            _ => $"{label}: adds a supporting touchpoint that helps the campaign stay present across the customer journey."
        };
    }

    private static IReadOnlyList<string> BuildSuccessMeasures(IReadOnlyList<RecommendationLineDocumentModel> mediaItems)
    {
        var channels = mediaItems
            .Select(item => NormalizeChannel(item.Channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var measures = new List<string>
        {
            "Supplier availability and booking confirmation for every selected placement."
        };

        if (channels.Contains("ooh", StringComparer.OrdinalIgnoreCase))
        {
            measures.Add("Site proof, flighting confirmation, and creative display checks for Billboards and Digital Screens.");
        }

        if (channels.Contains("radio", StringComparer.OrdinalIgnoreCase))
        {
            measures.Add("Booked radio spots, station log reconciliation, and response notes during the flight.");
        }

        if (channels.Contains("digital", StringComparer.OrdinalIgnoreCase))
        {
            measures.Add("Clicks, calls, forms, store visits, or Google Business actions from the digital layer.");
        }

        if (channels.Contains("tv", StringComparer.OrdinalIgnoreCase))
        {
            measures.Add("Confirmed TV insertions, proof of flighting, and response notes from the campaign window.");
        }

        if (channels.Contains("newspaper", StringComparer.OrdinalIgnoreCase))
        {
            measures.Add("Confirmed print insertion, publication proof, and response notes after publication.");
        }

        measures.Add("Post-campaign review comparing enquiries, footfall notes, and sales signals against the brief objective.");
        return measures.Take(5).ToArray();
    }

    private static string ResolveBusinessReference(RecommendationProposalCampaignContext campaign)
    {
        return !string.IsNullOrWhiteSpace(campaign.BusinessName)
            ? RecommendationPdfCopy.ToClientCopy(campaign.BusinessName)
            : (!string.IsNullOrWhiteSpace(campaign.ClientName) ? RecommendationPdfCopy.ToClientCopy(campaign.ClientName) : "this business");
    }

    private static string ResolveAreaPhrase(
        RecommendationProposalCampaignContext campaign,
        IReadOnlyList<RecommendationLineDocumentModel> mediaItems)
    {
        var itemAreas = mediaItems
            .Select(item => item.Region)
            .Where(area => !string.IsNullOrWhiteSpace(area))
            .Select(area => RecommendationPdfCopy.ToClientCopy(area))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        if (itemAreas.Length > 0)
        {
            return FormatList(itemAreas);
        }

        var campaignAreas = campaign.TargetAreas
            .Where(static area => !string.IsNullOrWhiteSpace(area))
            .Select(RecommendationPdfCopy.ToClientCopy)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        return campaignAreas.Length > 0 ? FormatList(campaignAreas) : "the selected market";
    }

    private static string ResolveChannelPhrase(IReadOnlyList<RecommendationLineDocumentModel> mediaItems)
    {
        var channels = mediaItems
            .Select(item => ResolveChannelLabel(NormalizeChannel(item.Channel)))
            .Where(static channel => !string.IsNullOrWhiteSpace(channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        return channels.Length > 0 ? FormatList(channels) : "the selected media mix";
    }

    private static string ResolvePrimaryChannel(IReadOnlyList<RecommendationLineDocumentModel> mediaItems)
    {
        return mediaItems
            .GroupBy(item => NormalizeChannel(item.Channel), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Sum(item => item.TotalCost))
            .ThenBy(group => GetChannelSortOrder(group.Key))
            .Select(group => group.Key)
            .FirstOrDefault(channel => !string.IsNullOrWhiteSpace(channel))
            ?? "media";
    }

    private static int CountDistinctChannels(IReadOnlyList<RecommendationLineDocumentModel> mediaItems)
    {
        return mediaItems
            .Select(item => NormalizeChannel(item.Channel))
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static string NormalizeChannel(string? channel)
    {
        var normalized = RecommendationPdfCopy.NormalizeRecommendationChannel(channel);
        return PlanningChannelSupport.IsOohFamilyChannel(normalized) ? PlanningChannelSupport.OohAlias : normalized;
    }

    private static string ResolveChannelLabel(string? channel)
    {
        var label = RecommendationPdfCopy.FormatChannelLabel(channel);
        return string.IsNullOrWhiteSpace(label) ? "Media" : label;
    }

    private static int GetChannelSortOrder(string? channel)
    {
        return NormalizeChannel(channel) switch
        {
            "ooh" => 0,
            "radio" => 1,
            "tv" => 2,
            "digital" => 3,
            "newspaper" => 4,
            _ => 9
        };
    }

    private static bool LooksLikePlannerSummary(string value)
    {
        return value.Contains("planned item", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Budget split target:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Requested target:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("target mix", StringComparison.OrdinalIgnoreCase)
            || value.Contains("mix target", StringComparison.OrdinalIgnoreCase)
            || value.Contains("allocation", StringComparison.OrdinalIgnoreCase)
            || value.Contains("weighted", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePlannerRationale(string value)
    {
        return value.Length < 35
            || value.Equals("Plan built within budget.", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Plan built within budget", StringComparison.OrdinalIgnoreCase)
            || value.Contains("prioritising geography fit", StringComparison.OrdinalIgnoreCase)
            || value.Contains("prioritizing geography fit", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Selected mix:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Requested target:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Budget allocation", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Strategy weighting", StringComparison.OrdinalIgnoreCase)
            || value.Contains("mix target", StringComparison.OrdinalIgnoreCase)
            || value.Contains("algorithm", StringComparison.OrdinalIgnoreCase)
            || value.Contains("calculated", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Selected by planner", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatList(IReadOnlyList<string> values)
    {
        var cleaned = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToArray();

        return cleaned.Length switch
        {
            0 => string.Empty,
            1 => cleaned[0],
            2 => $"{cleaned[0]} and {cleaned[1]}",
            _ => $"{string.Join(", ", cleaned.Take(cleaned.Length - 1))}, and {cleaned[^1]}"
        };
    }

    private static string StripPrefix(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Trim()
            : value;
    }
}
