using Advertified.App.Data.Entities;
using Advertified.App.Services;
using FluentAssertions;

namespace Advertified.App.Tests;

public class LeadEnrichmentSnapshotServiceTests
{
    [Fact]
    public void Build_BlocksWhenRequiredFieldsAreMissing()
    {
        var service = new LeadEnrichmentSnapshotService();
        var lead = new Lead
        {
            Id = 1001,
            Name = "Unknown Lead"
        };

        var snapshot = service.Build(
            lead,
            latestSignal: null,
            evidences: Array.Empty<LeadSignalEvidence>(),
            channelDetections: Array.Empty<LeadChannelDetectionResult>());

        snapshot.ConfidenceGate.IsBlocked.Should().BeTrue();
        snapshot.ConfidenceGate.MissingRequiredFields.Should().BeEquivalentTo(
            new[] { "Location", "Industry", "Channel activity" });
        snapshot.MissingFields.Should().Contain(new[] { "Location", "Industry", "Channel activity" });
        snapshot.ConfidenceScore.Should().Be(0m);
    }

    [Fact]
    public void Build_UsesDetectedEvidenceForRequiredFieldsAndPassesGate()
    {
        var service = new LeadEnrichmentSnapshotService();
        var lead = new Lead
        {
            Id = 1002,
            Name = "Thom Kight",
            Website = "http://www.thomkight.co.za/"
        };

        var evidences = new[]
        {
            new LeadSignalEvidence
            {
                LeadId = lead.Id,
                Channel = "website",
                SignalType = "google_business_profile_location",
                Source = "google_business_profile",
                Value = "Johannesburg",
                ObservedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            new LeadSignalEvidence
            {
                LeadId = lead.Id,
                Channel = "website",
                SignalType = "google_business_profile_category",
                Source = "google_business_profile",
                Value = "Funeral Director",
                ObservedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            new LeadSignalEvidence
            {
                LeadId = lead.Id,
                Channel = "website",
                SignalType = "website_language_detected",
                Source = "website_scan",
                Value = "English",
                ObservedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            }
        };

        var channelDetections = new[]
        {
            new LeadChannelDetectionResult
            {
                LeadId = lead.Id,
                Channel = "search",
                Score = 84,
                Confidence = "detected",
                Status = "strong",
                DominantReason = "Strong search signal evidence."
            }
        };

        var snapshot = service.Build(
            lead,
            latestSignal: null,
            evidences: evidences,
            channelDetections: channelDetections);

        snapshot.ConfidenceGate.IsBlocked.Should().BeFalse();
        snapshot.ConfidenceGate.MissingRequiredFields.Should().BeEmpty();

        snapshot.Fields.Should().ContainEquivalentOf(new LeadEnrichmentField
        {
            Key = "location",
            Label = "Location",
            Value = "Johannesburg",
            Confidence = "detected",
            Source = "google_business_profile",
            Reason = "Location extracted from external enrichment evidence.",
            Required = true
        });

        snapshot.Fields.Should().ContainEquivalentOf(new LeadEnrichmentField
        {
            Key = "industry",
            Label = "Industry",
            Value = "Funeral Director",
            Confidence = "detected",
            Source = "google_business_profile",
            Reason = "Industry resolved from enrichment evidence.",
            Required = true
        });

        snapshot.Fields.Should().ContainEquivalentOf(new LeadEnrichmentField
        {
            Key = "channel_activity",
            Label = "Channel activity",
            Value = "Search (84/100)",
            Confidence = "detected",
            Source = "channel_detection",
            Reason = "Strong search signal evidence.",
            Required = true
        });
    }

    [Fact]
    public void Build_AppliesFuneralAudienceAndBudgetHeuristics()
    {
        var service = new LeadEnrichmentSnapshotService();
        var lead = new Lead
        {
            Id = 1003,
            Name = "Thom Kight",
            Location = "Johannesburg",
            Category = "Funeral Services",
            Website = "thomkight.co.za"
        };

        var latestSignal = new Signal
        {
            LeadId = lead.Id,
            HasPromo = true
        };

        var snapshot = service.Build(
            lead,
            latestSignal: latestSignal,
            evidences: Array.Empty<LeadSignalEvidence>(),
            channelDetections: new[]
            {
                new LeadChannelDetectionResult
                {
                    LeadId = lead.Id,
                    Channel = "social",
                    Score = 65,
                    Confidence = "inferred",
                    Status = "strong",
                    DominantReason = "Social activity signals were found."
                }
            });

        snapshot.Fields.Should().ContainSingle(field => field.Key == "target_audience")
            .Which.Value.Should().Contain("Family decision-makers");

        snapshot.Fields.Should().ContainSingle(field => field.Key == "budget_tier")
            .Which.Value.Should().Be("Growth SME");

        snapshot.Fields.Should().ContainSingle(field => field.Key == "language")
            .Which.Confidence.Should().Be("inferred");
    }
}
