using System.Text.Json;
using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class AdminLeadIndustryPolicyService : IAdminLeadIndustryPolicyService
{
    private readonly AppDbContext _db;

    public AdminLeadIndustryPolicyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task CreateAsync(CreateAdminLeadIndustryPolicyRequest request, CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        if (await _db.LeadIndustryPolicySettings.AnyAsync(x => x.Key == normalized.Key, cancellationToken))
        {
            throw new InvalidOperationException($"A lead industry policy with key '{normalized.Key}' already exists.");
        }

        _db.LeadIndustryPolicySettings.Add(MapEntity(new LeadIndustryPolicySetting(), normalized));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(string key, UpdateAdminLeadIndustryPolicyRequest request, CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeKey(key);
        var entity = await _db.LeadIndustryPolicySettings.FirstOrDefaultAsync(x => x.Key == normalizedKey, cancellationToken);
        if (entity is null)
        {
            throw new InvalidOperationException($"Lead industry policy '{normalizedKey}' was not found.");
        }

        MapEntity(entity, Normalize(request));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeKey(key);
        var entity = await _db.LeadIndustryPolicySettings.FirstOrDefaultAsync(x => x.Key == normalizedKey, cancellationToken);
        if (entity is null)
        {
            throw new InvalidOperationException($"Lead industry policy '{normalizedKey}' was not found.");
        }

        _db.LeadIndustryPolicySettings.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static LeadIndustryPolicySetting MapEntity(LeadIndustryPolicySetting entity, NormalizedPolicyRequest request)
    {
        entity.Key = request.Key;
        entity.Name = request.Name;
        entity.ObjectiveOverride = request.ObjectiveOverride;
        entity.PreferredTone = request.PreferredTone;
        entity.PreferredChannelsJson = JsonSerializer.Serialize(request.PreferredChannels);
        entity.Cta = request.Cta;
        entity.MessagingAngle = request.MessagingAngle;
        entity.GuardrailsJson = JsonSerializer.Serialize(request.Guardrails);
        entity.AdditionalGap = request.AdditionalGap;
        entity.AdditionalOutcome = request.AdditionalOutcome;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        return entity;
    }

    private static NormalizedPolicyRequest Normalize(CreateAdminLeadIndustryPolicyRequest request)
    {
        return NormalizeCore(
            request.Key,
            request.Name,
            request.ObjectiveOverride,
            request.PreferredTone,
            request.PreferredChannels,
            request.Cta,
            request.MessagingAngle,
            request.Guardrails,
            request.AdditionalGap,
            request.AdditionalOutcome,
            request.SortOrder,
            request.IsActive);
    }

    private static NormalizedPolicyRequest Normalize(UpdateAdminLeadIndustryPolicyRequest request)
    {
        return NormalizeCore(
            request.Key,
            request.Name,
            request.ObjectiveOverride,
            request.PreferredTone,
            request.PreferredChannels,
            request.Cta,
            request.MessagingAngle,
            request.Guardrails,
            request.AdditionalGap,
            request.AdditionalOutcome,
            request.SortOrder,
            request.IsActive);
    }

    private static NormalizedPolicyRequest NormalizeCore(
        string key,
        string name,
        string? objectiveOverride,
        string? preferredTone,
        IReadOnlyList<string>? preferredChannels,
        string cta,
        string messagingAngle,
        IReadOnlyList<string>? guardrails,
        string additionalGap,
        string additionalOutcome,
        int sortOrder,
        bool isActive)
    {
        var normalizedKey = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            throw new InvalidOperationException("Policy key is required.");
        }

        var normalizedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Policy name is required.");
        }

        var normalizedCta = cta?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedCta))
        {
            throw new InvalidOperationException("CTA is required.");
        }

        var normalizedMessagingAngle = messagingAngle?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedMessagingAngle))
        {
            throw new InvalidOperationException("Messaging angle is required.");
        }

        return new NormalizedPolicyRequest(
            normalizedKey,
            normalizedName,
            string.IsNullOrWhiteSpace(objectiveOverride) ? null : objectiveOverride.Trim(),
            string.IsNullOrWhiteSpace(preferredTone) ? null : preferredTone.Trim(),
            NormalizeValues(preferredChannels),
            normalizedCta,
            normalizedMessagingAngle,
            NormalizeValues(guardrails),
            additionalGap?.Trim() ?? string.Empty,
            additionalOutcome?.Trim() ?? string.Empty,
            sortOrder,
            isActive);
    }

    private static string NormalizeKey(string? key)
    {
        return (key ?? string.Empty).Trim();
    }

    private static IReadOnlyList<string> NormalizeValues(IReadOnlyList<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record NormalizedPolicyRequest(
        string Key,
        string Name,
        string? ObjectiveOverride,
        string? PreferredTone,
        IReadOnlyList<string> PreferredChannels,
        string Cta,
        string MessagingAngle,
        IReadOnlyList<string> Guardrails,
        string AdditionalGap,
        string AdditionalOutcome,
        int SortOrder,
        bool IsActive);
}
