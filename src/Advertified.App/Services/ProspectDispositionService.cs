using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class ProspectDispositionService : IProspectDispositionService
{
    private const int MaxNotesLength = 2000;
    private readonly FormOptionsService _formOptionsService;

    public ProspectDispositionService(FormOptionsService formOptionsService)
    {
        _formOptionsService = formOptionsService;
    }

    public async Task CloseAsync(
        Campaign campaign,
        Guid actorUserId,
        UserRole actorRole,
        string reasonCode,
        string? notes,
        CancellationToken cancellationToken)
    {
        EnsureAgentCanMutate(campaign, actorUserId, actorRole);

        if (!ProspectCampaignPolicy.IsProspectiveCampaign(campaign))
        {
            throw new InvalidOperationException("Only prospective campaigns can be closed.");
        }

        if (ProspectCampaignPolicy.IsClosed(campaign))
        {
            throw new InvalidOperationException("This prospect is already closed.");
        }

        var normalizedReasonCode = reasonCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReasonCode))
        {
            throw new InvalidOperationException("A close reason is required.");
        }

        var reasonAllowed = await _formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.ProspectDispositionReasons, normalizedReasonCode, cancellationToken);
        if (!reasonAllowed)
        {
            throw new InvalidOperationException("The selected close reason is no longer available.");
        }

        var normalizedNotes = NormalizeNotes(notes);
        var now = DateTime.UtcNow;
        campaign.ProspectDispositionStatus = ProspectDispositionStatuses.Closed;
        campaign.ProspectDispositionReason = normalizedReasonCode;
        campaign.ProspectDispositionNotes = normalizedNotes;
        campaign.ProspectDispositionClosedAt = now;
        campaign.ProspectDispositionClosedByUserId = actorUserId;
        campaign.UpdatedAt = now;
    }

    public Task ReopenAsync(Campaign campaign, Guid actorUserId, UserRole actorRole, CancellationToken cancellationToken)
    {
        EnsureAgentCanMutate(campaign, actorUserId, actorRole);

        if (!ProspectCampaignPolicy.IsProspectiveCampaign(campaign))
        {
            throw new InvalidOperationException("Only prospective campaigns can be reopened.");
        }

        if (!ProspectCampaignPolicy.IsClosed(campaign))
        {
            throw new InvalidOperationException("This prospect is already open.");
        }

        var now = DateTime.UtcNow;
        campaign.ProspectDispositionStatus = ProspectDispositionStatuses.Open;
        campaign.ProspectDispositionReason = null;
        campaign.ProspectDispositionNotes = null;
        campaign.ProspectDispositionClosedAt = null;
        campaign.ProspectDispositionClosedByUserId = null;
        campaign.UpdatedAt = now;
        return Task.CompletedTask;
    }

    private static void EnsureAgentCanMutate(Campaign campaign, Guid actorUserId, UserRole actorRole)
    {
        if (actorRole == UserRole.Agent && campaign.AssignedAgentUserId != actorUserId)
        {
            throw new InvalidOperationException("Only the assigned agent can update this prospect.");
        }
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var normalized = notes.Trim();
        if (normalized.Length > MaxNotesLength)
        {
            throw new InvalidOperationException($"Close notes cannot exceed {MaxNotesLength} characters.");
        }

        return normalized;
    }
}
