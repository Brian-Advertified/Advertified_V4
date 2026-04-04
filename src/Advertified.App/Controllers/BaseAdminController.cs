using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Advertified.App.Controllers;

/// <summary>
/// Base controller for admin endpoints with shared authorization, audit logging, and utility methods.
/// Derived controllers handle specific functional domains (outlets, users, AI, geography, etc.)
/// </summary>
public abstract class BaseAdminController : ControllerBase
{
    protected static readonly string[] AllowedVoicePackPricingTiers = { "standard", "premium", "exclusive" };
    protected readonly AppDbContext Db;
    protected readonly ICurrentUserAccessor CurrentUserAccessor;
    protected readonly IChangeAuditService ChangeAuditService;

    protected BaseAdminController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService)
    {
        Db = db;
        CurrentUserAccessor = currentUserAccessor;
        ChangeAuditService = changeAuditService;
    }

    /// <summary>
    /// Verifies the current user is authenticated and has admin role.
    /// Returns Unauthorized() if not authenticated, Forbidden if not admin, or null if authorized.
    /// </summary>
    protected async Task<ActionResult?> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = await CurrentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized();
        }

        var currentUser = await Db.UserAccounts.FindAsync(new object[] { currentUserId }, cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        if (currentUser.Role != UserRole.Admin)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return null;
    }

    protected async Task WriteChangeAuditAsync(
        string action,
        string entityType,
        string entityId,
        string? entityLabel,
        string summary,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var currentUserId = await CurrentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        await ChangeAuditService.WriteAsync(currentUserId, "admin", action, entityType, entityId, entityLabel, summary, metadata, cancellationToken);
    }

    protected Task WriteChangeAuditAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string entityId,
        string? entityLabel,
        string summary,
        object? metadata,
        CancellationToken cancellationToken)
    {
        return ChangeAuditService.WriteAsync(actorUserId, "admin", action, entityType, entityId, entityLabel, summary, metadata, cancellationToken);
    }

    protected static string FormatAuditSource(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return "System change";
        }

        var normalizedScope = scope.Trim().ToLowerInvariant();
        return $"{char.ToUpperInvariant(normalizedScope[0])}{normalizedScope.Substring(1)} change";
    }

    protected static string SerializeList(IEnumerable<string>? values)
    {
        var normalized = (values ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return JsonSerializer.Serialize(normalized);
    }

    protected static string[] DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    protected static string NormalizePricingTier(string? value)
    {
        var normalized = (value ?? "standard").Trim().ToLowerInvariant();
        if (!AllowedVoicePackPricingTiers.Contains(normalized))
        {
            throw new InvalidOperationException("Pricing tier is invalid.");
        }

        return normalized;
    }

    protected static string? TrimOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    protected static string RequireValue(string? value, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return normalized;
    }

    protected static string NormalizeEmail(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    protected static UserRole ParseUserRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new InvalidOperationException("Role is required.");
        }

        if (!Enum.TryParse<UserRole>(role.Trim(), ignoreCase: true, out var parsed))
        {
            throw new InvalidOperationException("Invalid user role.");
        }

        return parsed;
    }

    protected static AccountStatus ParseAccountStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new InvalidOperationException("Account status is required.");
        }

        if (!Enum.TryParse<AccountStatus>(status.Trim(), ignoreCase: true, out var parsed))
        {
            throw new InvalidOperationException("Invalid account status.");
        }

        return parsed;
    }

    protected async Task<Dictionary<string, string>> GetAreaLabelsByCodeAsync(CancellationToken cancellationToken)
    {
        var rows = await Db.Database
            .SqlQueryRaw<AreaLabelLookup>("select cluster_code as Code, display_name as Label from package_area_profiles where is_active = true;")
            .ToListAsync();

        return rows.ToDictionary(x => x.Code, x => x.Label, StringComparer.OrdinalIgnoreCase);
    }

    protected async Task SyncAgentAreaAssignmentsAsync(Guid userId, UserRole role, IReadOnlyList<string>? areaCodes, CancellationToken cancellationToken)
    {
        areaCodes ??= Array.Empty<string>();

        var existingAssignments = await Db.AgentAreaAssignments
            .Where(x => x.AgentUserId == userId)
            .ToArrayAsync(cancellationToken);

        if (role != UserRole.Agent)
        {
            if (existingAssignments.Length > 0)
            {
                Db.AgentAreaAssignments.RemoveRange(existingAssignments);
                await Db.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var normalizedCodes = areaCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var validAreaCodes = await Db.Database
            .SqlQueryRaw<AreaCodeLookup>("select cluster_code as \"Code\" from package_area_profiles where is_active = true")
            .Select(x => x.Code)
            .ToArrayAsync(cancellationToken);

        var codesToAdd = normalizedCodes.Except(existingAssignments.Select(x => x.AreaCode), StringComparer.OrdinalIgnoreCase).ToArray();
        var codesToRemove = existingAssignments.Where(x => !normalizedCodes.Contains(x.AreaCode, StringComparer.OrdinalIgnoreCase)).ToArray();

        foreach (var code in codesToAdd)
        {
            if (!validAreaCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Area code {code} is not valid.");
            }

            var assignment = new AgentAreaAssignment
            {
                Id = Guid.NewGuid(),
                AgentUserId = userId,
                AreaCode = code,
                CreatedAt = DateTime.UtcNow
            };
            Db.AgentAreaAssignments.Add(assignment);
        }

        if (codesToRemove.Length > 0)
        {
            Db.AgentAreaAssignments.RemoveRange(codesToRemove);
        }

        await Db.SaveChangesAsync(cancellationToken);
    }

    protected static AdminUserResponse MapAdminUser(UserAccount user, IReadOnlyList<AgentAreaAssignment> assignments, Dictionary<string, string> areaLabels)
    {
        var assignedAreaCodes = assignments
            .Where(x => x.AgentUserId == user.Id)
            .Select(x => x.AreaCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

        return new AdminUserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = FormatUserRole(user.Role),
            AccountStatus = user.AccountStatus.ToString(),
            IsSaCitizen = user.IsSaCitizen,
            EmailVerified = user.EmailVerified,
            PhoneVerified = user.PhoneVerified,
            AssignedAreaCodes = assignedAreaCodes,
            AssignedAreaLabels = assignedAreaCodes.Select(code => areaLabels.TryGetValue(code, out var label) ? label : code).ToArray(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private static string FormatUserRole(UserRole role)
    {
        return role switch
        {
            UserRole.CreativeDirector => "creative_director",
            UserRole.Agent => "agent",
            UserRole.Client => "client",
            UserRole.Admin => "admin",
            _ => "unknown"
        };
    }

    protected static AdminAiVoiceProfileResponse MapAdminAiVoiceProfile(AiVoiceProfile row)
    {
        return new AdminAiVoiceProfileResponse
        {
            Id = row.Id,
            Provider = row.Provider,
            Label = row.Label,
            VoiceId = row.VoiceId,
            Language = row.Language,
            IsActive = row.IsActive,
            SortOrder = row.SortOrder,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };
    }

    protected static AdminAiVoicePackResponse MapAdminAiVoicePack(AiVoicePack row)
    {
        return new AdminAiVoicePackResponse
        {
            Id = row.Id,
            Provider = row.Provider,
            Name = row.Name,
            Accent = row.Accent,
            Language = row.Language,
            Tone = row.Tone,
            Persona = row.Persona,
            UseCases = DeserializeList(row.UseCasesJson),
            VoiceId = row.VoiceId,
            SampleAudioUrl = row.SampleAudioUrl,
            PromptTemplate = row.PromptTemplate,
            PricingTier = row.PricingTier,
            IsClientSpecific = row.IsClientSpecific,
            ClientUserId = row.ClientUserId,
            IsClonedVoice = row.IsClonedVoice,
            AudienceTags = DeserializeList(row.AudienceTagsJson),
            ObjectiveTags = DeserializeList(row.ObjectiveTagsJson),
            IsActive = row.IsActive,
            SortOrder = row.SortOrder,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };
    }

    protected static AdminAiVoiceTemplateResponse MapAdminAiVoiceTemplate(AiVoicePromptTemplate row)
    {
        return new AdminAiVoiceTemplateResponse
        {
            Id = row.Id,
            TemplateNumber = row.TemplateNumber,
            Category = row.Category,
            Name = row.Name,
            PromptTemplate = row.PromptTemplate,
            PrimaryVoicePackName = row.PrimaryVoicePackName,
            FallbackVoicePackNames = DeserializeList(row.FallbackVoicePackNamesJson),
            IsActive = row.IsActive,
            SortOrder = row.SortOrder,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };
    }

    #region Data Lookup Types

    private class AreaLabelLookup
    {
        public string Code { get; set; } = null!;
        public string Label { get; set; } = null!;
    }

    private class AreaCodeLookup
    {
        public string Code { get; set; } = null!;
    }

    #endregion
}
