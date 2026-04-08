using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class SignalCollectorService : ISignalCollectorService
{
    private readonly AppDbContext _db;
    private readonly IReadOnlyList<ILeadSignalEvidenceProvider> _evidenceProviders;

    public SignalCollectorService(
        AppDbContext db,
        IEnumerable<ILeadSignalEvidenceProvider> evidenceProviders)
    {
        _db = db;
        _evidenceProviders = evidenceProviders.ToList();
    }

    public async Task<Signal> CollectAsync(int leadId, CancellationToken cancellationToken)
    {
        var lead = await _db.Leads
            .FirstOrDefaultAsync(x => x.Id == leadId, cancellationToken)
            ?? throw new InvalidOperationException("Lead not found.");

        var evidenceInputs = new List<LeadSignalEvidenceInput>();
        foreach (var provider in _evidenceProviders)
        {
            var providerEvidence = await provider.CollectAsync(lead, cancellationToken);
            if (providerEvidence.Count == 0)
            {
                continue;
            }

            evidenceInputs.AddRange(providerEvidence);
        }

        var now = DateTime.UtcNow;
        var deduplicatedEvidence = evidenceInputs
            .Where(item => !string.IsNullOrWhiteSpace(item.Channel) && !string.IsNullOrWhiteSpace(item.SignalType))
            .GroupBy(item => new
            {
                Channel = item.Channel.Trim().ToLowerInvariant(),
                SignalType = item.SignalType.Trim().ToLowerInvariant(),
                Source = item.Source.Trim().ToLowerInvariant(),
                EvidenceUrl = item.EvidenceUrl?.Trim() ?? string.Empty,
                Value = item.Value.Trim()
            })
            .Select(group => group.First())
            .ToArray();

        var signal = new Signal
        {
            LeadId = lead.Id,
            HasPromo = deduplicatedEvidence.Any(item => item.SignalType.Equals("campaign_mention", StringComparison.OrdinalIgnoreCase)),
            HasMetaAds = deduplicatedEvidence.Any(item =>
                item.SignalType.Equals("meta_pixel_detected", StringComparison.OrdinalIgnoreCase)
                || item.SignalType.Equals("meta_ad_library_active_ads", StringComparison.OrdinalIgnoreCase)),
            WebsiteUpdatedRecently = deduplicatedEvidence.Any(item => item.SignalType.Equals("fresh_website", StringComparison.OrdinalIgnoreCase)),
            CreatedAt = now
        };

        _db.Signals.Add(signal);
        await _db.SaveChangesAsync(cancellationToken);

        if (deduplicatedEvidence.Length > 0)
        {
            var evidenceRows = deduplicatedEvidence.Select(item =>
            {
                var freshnessMultiplier = LeadEvidenceScoring.ResolveFreshnessMultiplier(item.ObservedAtUtc, now);
                var effectiveWeight = LeadEvidenceScoring.ResolveEffectiveWeight(item, freshnessMultiplier);

                return new LeadSignalEvidence
                {
                    LeadId = lead.Id,
                    SignalId = signal.Id,
                    Channel = item.Channel.Trim().ToLowerInvariant(),
                    SignalType = item.SignalType.Trim().ToLowerInvariant(),
                    Source = item.Source.Trim().ToLowerInvariant(),
                    Confidence = string.IsNullOrWhiteSpace(item.Confidence) ? "weakly_inferred" : item.Confidence.Trim().ToLowerInvariant(),
                    Weight = item.Weight,
                    ReliabilityMultiplier = item.ReliabilityMultiplier,
                    FreshnessMultiplier = freshnessMultiplier,
                    EffectiveWeight = effectiveWeight,
                    IsPositive = item.IsPositive,
                    ObservedAt = item.ObservedAtUtc,
                    EvidenceUrl = string.IsNullOrWhiteSpace(item.EvidenceUrl) ? null : item.EvidenceUrl.Trim(),
                    Value = item.Value.Trim(),
                    CreatedAt = now
                };
            }).ToArray();

            _db.LeadSignalEvidences.AddRange(evidenceRows);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return signal;
    }
}
