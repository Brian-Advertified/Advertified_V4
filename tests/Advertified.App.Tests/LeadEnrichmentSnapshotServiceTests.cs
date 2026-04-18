using Advertified.App.Data.Entities;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
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
            new[] { "Location", "Industry" });
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
            Required = false
        });
    }

    [Fact]
    public void Build_AppliesFuneralAudienceAndBudgetHeuristics()
    {
        var service = new LeadEnrichmentSnapshotService(new StubResolvedGeocodingService(), new StubLeadMasterDataService());
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

    [Fact]
    public void Build_DetectsPrimaryAndSecondaryLanguages_FromEvidence()
    {
        var service = new LeadEnrichmentSnapshotService(new StubResolvedGeocodingService(), new StubLeadMasterDataService());
        var lead = new Lead
        {
            Id = 1004,
            Name = "Bilingual Lead",
            Category = "Retail",
            Location = "Pretoria"
        };

        var now = DateTime.UtcNow;
        var evidences = new[]
        {
            new LeadSignalEvidence
            {
                LeadId = lead.Id,
                Channel = "website",
                SignalType = "website_language_detected",
                Source = "website_scan",
                Value = "English",
                ObservedAt = now,
                CreatedAt = now
            },
            new LeadSignalEvidence
            {
                LeadId = lead.Id,
                Channel = "website",
                SignalType = "social_language_detected",
                Source = "social_scan",
                Value = "Afrikaans",
                ObservedAt = now.AddMinutes(-1),
                CreatedAt = now.AddMinutes(-1)
            }
        };

        var snapshot = service.Build(
            lead,
            latestSignal: null,
            evidences: evidences,
            channelDetections: new[]
            {
                new LeadChannelDetectionResult
                {
                    LeadId = lead.Id,
                    Channel = "search",
                    Score = 80,
                    Confidence = "detected",
                    Status = "strong",
                    DominantReason = "Active search indicators."
                }
            });

        snapshot.Fields.Should().ContainSingle(field => field.Key == "language")
            .Which.Value.Should().Be("English");
        snapshot.Fields.Should().ContainSingle(field => field.Key == "secondary_language")
            .Which.Value.Should().Be("Afrikaans");
    }

    private sealed class StubResolvedGeocodingService : IGeocodingService
    {
        public GeocodingResolution ResolveLocation(string? rawLocation)
        {
            return new GeocodingResolution
            {
                IsResolved = true,
                CanonicalLocation = rawLocation?.Trim() ?? "Johannesburg",
                Latitude = -26.2041d,
                Longitude = 28.0473d,
                Source = "master_locations"
            };
        }

        public GeocodingResolution ResolveCampaignTarget(Advertified.App.Contracts.Campaigns.CampaignPlanningRequest request)
        {
            return new GeocodingResolution
            {
                IsResolved = true,
                CanonicalLocation = "Johannesburg",
                Latitude = -26.2041d,
                Longitude = 28.0473d,
                Source = "master_locations"
            };
        }
    }

    private sealed class StubLeadMasterDataService : ILeadMasterDataService
    {
        public LeadMasterTokenSet GetTokenSet() => new();

        public MasterLocationMatch? ResolveLocation(string? value) => null;

        public MasterIndustryMatch? ResolveIndustry(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Contains("funeral", StringComparison.OrdinalIgnoreCase))
            {
                return new MasterIndustryMatch
                {
                    Code = LeadCanonicalValues.IndustryCodes.FuneralServices,
                    Label = "Funeral Services"
                };
            }

            return null;
        }

        public MasterIndustryMatch? ResolveIndustryFromHints(IReadOnlyList<string> hints) => null;

        public MasterLanguageMatch? ResolveLanguage(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (value.Contains("english", StringComparison.OrdinalIgnoreCase))
            {
                return new MasterLanguageMatch { Code = "en", Label = "English" };
            }

            if (value.Contains("afrikaans", StringComparison.OrdinalIgnoreCase))
            {
                return new MasterLanguageMatch { Code = "af", Label = "Afrikaans" };
            }

            return null;
        }
    }
}
