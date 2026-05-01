using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Tests;

public class LeadProposalConfidenceGateServiceTests
{
    [Fact]
    public async Task EnsureCampaignReadyAsync_AllowsNonLeadCampaign()
    {
        await using var db = LeadIntelligenceTestHelpers.CreateDbContext();
        var campaignId = Guid.NewGuid();
        await SeedCampaignAsync(db, campaignId, specialRequirements: "Standard recommendation context.");

        var service = CreateService(db);

        var act = async () => await service.EnsureCampaignReadyAsync(campaignId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCampaignReadyAsync_BlocksLeadCampaignWithoutSourceLeadReference()
    {
        await using var db = LeadIntelligenceTestHelpers.CreateDbContext();
        var campaignId = Guid.NewGuid();
        await SeedCampaignAsync(
            db,
            campaignId,
            specialRequirements: "Lead intelligence summary:\nSignals found but no source lead marker.");

        var service = CreateService(db);

        var act = async () => await service.EnsureCampaignReadyAsync(campaignId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing a source lead reference*");
    }

    [Fact]
    public async Task EnsureCampaignReadyAsync_BlocksWhenLeadConfidenceGateFails()
    {
        await using var db = LeadIntelligenceTestHelpers.CreateDbContext();
        var campaignId = Guid.NewGuid();
        const int leadId = 7021;

        db.Leads.Add(new Lead
        {
            Id = leadId,
            Name = "Solo Shoes",
            Website = "soloshoes.co.za",
            Location = "Johannesburg",
            Category = string.Empty,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        });

        await SeedCampaignAsync(
            db,
            campaignId,
            specialRequirements: $"Lead source id: {leadId}\n\nLead intelligence summary:\nSignals found.");

        var service = CreateService(db);

        var act = async () => await service.EnsureCampaignReadyAsync(campaignId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Recommendation halted. Missing required confidence fields*");
    }

    [Fact]
    public async Task EnsureCampaignReadyAsync_AllowsLeadCampaignWithoutChannelActivity_WhenLocationAndIndustryExist()
    {
        await using var db = LeadIntelligenceTestHelpers.CreateDbContext();
        var campaignId = Guid.NewGuid();
        const int leadId = 7022;
        var now = DateTime.UtcNow;

        db.Leads.Add(new Lead
        {
            Id = leadId,
            Name = "Solo Shoes",
            Website = "soloshoes.co.za",
            Location = "Johannesburg",
            Category = "Retail",
            CreatedAt = now.AddDays(-3)
        });

        const int signalId = 8801;
        const long locationEvidenceId = 99001;
        const long industryEvidenceId = 99002;
        db.Signals.Add(new Signal
        {
            Id = signalId,
            LeadId = leadId,
            CreatedAt = now.AddHours(-2)
        });

        db.LeadSignalEvidences.AddRange(
            new LeadSignalEvidence
            {
                Id = locationEvidenceId,
                LeadId = leadId,
                SignalId = signalId,
                Channel = "website",
                SignalType = "google_business_profile_location",
                Source = "google_business_profile",
                Value = "Johannesburg",
                ObservedAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-2)
            },
            new LeadSignalEvidence
            {
                Id = industryEvidenceId,
                LeadId = leadId,
                SignalId = signalId,
                Channel = "website",
                SignalType = "google_business_profile_category",
                Source = "google_business_profile",
                Value = "Retail",
                ObservedAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-2)
            });

        await SeedCampaignAsync(
            db,
            campaignId,
            specialRequirements: $"Lead source id: {leadId}\n\nLead intelligence summary:\nSignals found.");

        var service = CreateService(db);

        var act = async () => await service.EnsureCampaignReadyAsync(campaignId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static LeadProposalConfidenceGateService CreateService(AppDbContext db)
    {
        return new LeadProposalConfidenceGateService(
            db,
            new LeadChannelDetectionService(),
            new LeadEnrichmentSnapshotService(),
            new StubLeadIndustryContextResolver());
    }

    private sealed class StubLeadIndustryContextResolver : ILeadIndustryContextResolver
    {
        public LeadIndustryContext ResolveFromCategory(string? category) => new();
        public IReadOnlyList<LeadIndustryContext> ResolveFromHints(IReadOnlyList<string> hints) => Array.Empty<LeadIndustryContext>();
    }

    private static async Task SeedCampaignAsync(AppDbContext db, Guid campaignId, string specialRequirements)
    {
        var packageBandId = Guid.NewGuid();
        var packageOrderId = Guid.NewGuid();

        db.PackageBands.Add(new PackageBand
        {
            Id = packageBandId,
            Name = "Scale",
            Code = "scale",
            MinBudget = 40_000m,
            MaxBudget = 80_000m,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        });

        db.PackageOrders.Add(new PackageOrder
        {
            Id = packageOrderId,
            PackageBandId = packageBandId,
            OrderIntent = OrderIntentValues.Prospect,
            Amount = 50_000m,
            SelectedBudget = 50_000m,
            PaymentStatus = "pending",
            Currency = "ZAR",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        });

        db.Campaigns.Add(new Campaign
        {
            Id = campaignId,
            PackageBandId = packageBandId,
            PackageOrderId = packageOrderId,
            Status = "awaiting_purchase",
            PlanningMode = "ai_assisted",
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        });

        db.CampaignBriefs.Add(new CampaignBrief
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            Objective = "Awareness",
            GeographyScope = "local",
            SpecialRequirements = specialRequirements,
            OpenToUpsell = false,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        });

        await db.SaveChangesAsync();
    }
}
