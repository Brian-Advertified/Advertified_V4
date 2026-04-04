using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/ai")]
public sealed class AdminAiVoiceCatalogController : BaseAdminController
{
    public AdminAiVoiceCatalogController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService)
        : base(db, currentUserAccessor, changeAuditService)
    {
    }

    [HttpGet("voices")]
    public async Task<ActionResult<IReadOnlyList<AdminAiVoiceProfileResponse>>> GetVoiceProfiles(
        [FromQuery] string? provider,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "ElevenLabs" : provider.Trim();
        var rows = await Db.AiVoiceProfiles
            .AsNoTracking()
            .Where(item => item.Provider == normalizedProvider)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Label)
            .ToArrayAsync(cancellationToken);

        return Ok(rows.Select(MapAdminAiVoiceProfile).ToArray());
    }

    [HttpPost("voices")]
    public async Task<ActionResult<AdminAiVoiceProfileResponse>> CreateVoiceProfile(
        [FromBody] UpsertAdminAiVoiceProfileRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var provider = string.IsNullOrWhiteSpace(request.Provider) ? "ElevenLabs" : request.Provider.Trim();
            var label = RequireValue(request.Label, "Label");
            var voiceId = RequireValue(request.VoiceId, "Voice ID");

            var exists = await Db.AiVoiceProfiles.AnyAsync(
                item => item.Provider == provider && item.Label == label,
                cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("A voice profile with this label already exists for the provider.");
            }

            var now = DateTime.UtcNow;
            var row = new AiVoiceProfile
            {
                Id = Guid.NewGuid(),
                Provider = provider,
                Label = label,
                VoiceId = voiceId,
                Language = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language.Trim(),
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                CreatedAt = now,
                UpdatedAt = now
            };

            Db.AiVoiceProfiles.Add(row);
            await Db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "create",
                "ai_voice_profile",
                row.Id.ToString(),
                row.Label,
                $"Created AI voice profile {row.Label}.",
                new { row.Provider, row.Label, row.VoiceId, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoiceProfile(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("voices/{id:guid}")]
    public async Task<ActionResult<AdminAiVoiceProfileResponse>> UpdateVoiceProfile(
        Guid id,
        [FromBody] UpsertAdminAiVoiceProfileRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var row = await Db.AiVoiceProfiles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (row is null)
            {
                return NotFound();
            }

            var provider = string.IsNullOrWhiteSpace(request.Provider) ? "ElevenLabs" : request.Provider.Trim();
            var label = RequireValue(request.Label, "Label");
            var voiceId = RequireValue(request.VoiceId, "Voice ID");

            var duplicate = await Db.AiVoiceProfiles.AnyAsync(
                item => item.Id != id && item.Provider == provider && item.Label == label,
                cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("A voice profile with this label already exists for the provider.");
            }

            row.Provider = provider;
            row.Label = label;
            row.VoiceId = voiceId;
            row.Language = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language.Trim();
            row.IsActive = request.IsActive;
            row.SortOrder = request.SortOrder;
            row.UpdatedAt = DateTime.UtcNow;

            await Db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "ai_voice_profile",
                row.Id.ToString(),
                row.Label,
                $"Updated AI voice profile {row.Label}.",
                new { row.Provider, row.Label, row.VoiceId, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoiceProfile(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("voices/{id:guid}")]
    public async Task<IActionResult> DeleteVoiceProfile(Guid id, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var row = await Db.AiVoiceProfiles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        Db.AiVoiceProfiles.Remove(row);
        await Db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "delete",
            "ai_voice_profile",
            id.ToString(),
            row.Label,
            $"Deleted AI voice profile {row.Label}.",
            new { row.Provider, row.Label },
            cancellationToken);
        return NoContent();
    }

    [HttpGet("voice-packs")]
    public async Task<ActionResult<IReadOnlyList<AdminAiVoicePackResponse>>> GetVoicePacks(
        [FromQuery] string? provider,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "ElevenLabs" : provider.Trim();
        var rows = await Db.AiVoicePacks
            .AsNoTracking()
            .Where(item => item.Provider == normalizedProvider)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToArrayAsync(cancellationToken);

        return Ok(rows.Select(MapAdminAiVoicePack).ToArray());
    }

    [HttpPost("voice-packs")]
    public async Task<ActionResult<AdminAiVoicePackResponse>> CreateVoicePack(
        [FromBody] UpsertAdminAiVoicePackRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var provider = string.IsNullOrWhiteSpace(request.Provider) ? "ElevenLabs" : request.Provider.Trim();
            var name = RequireValue(request.Name, "Name");
            var voiceId = RequireValue(request.VoiceId, "Voice ID");
            var promptTemplate = RequireValue(request.PromptTemplate, "Prompt template");
            var pricingTier = NormalizePricingTier(request.PricingTier);
            if (request.IsClientSpecific && !request.ClientUserId.HasValue)
            {
                throw new InvalidOperationException("Client user id is required for client-specific voice packs.");
            }

            var exists = await Db.AiVoicePacks.AnyAsync(
                item => item.Provider == provider && item.Name == name,
                cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("A voice pack with this name already exists for the provider.");
            }

            var now = DateTime.UtcNow;
            var row = new AiVoicePack
            {
                Id = Guid.NewGuid(),
                Provider = provider,
                Name = name,
                Accent = TrimOrNull(request.Accent),
                Language = TrimOrNull(request.Language),
                Tone = TrimOrNull(request.Tone),
                Persona = TrimOrNull(request.Persona),
                UseCasesJson = SerializeList(request.UseCases),
                VoiceId = voiceId,
                SampleAudioUrl = TrimOrNull(request.SampleAudioUrl),
                PromptTemplate = promptTemplate,
                PricingTier = pricingTier,
                IsClientSpecific = request.IsClientSpecific,
                ClientUserId = request.ClientUserId,
                IsClonedVoice = request.IsClonedVoice,
                AudienceTagsJson = SerializeList(request.AudienceTags),
                ObjectiveTagsJson = SerializeList(request.ObjectiveTags),
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                CreatedAt = now,
                UpdatedAt = now
            };

            Db.AiVoicePacks.Add(row);
            await Db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "create",
                "ai_voice_pack",
                row.Id.ToString(),
                row.Name,
                $"Created AI voice pack {row.Name}.",
                new { row.Provider, row.Name, row.PricingTier, row.IsClientSpecific, row.ClientUserId, row.IsClonedVoice, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoicePack(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("voice-packs/{id:guid}")]
    public async Task<ActionResult<AdminAiVoicePackResponse>> UpdateVoicePack(
        Guid id,
        [FromBody] UpsertAdminAiVoicePackRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var row = await Db.AiVoicePacks.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (row is null)
            {
                return NotFound();
            }

            var provider = string.IsNullOrWhiteSpace(request.Provider) ? "ElevenLabs" : request.Provider.Trim();
            var name = RequireValue(request.Name, "Name");
            var voiceId = RequireValue(request.VoiceId, "Voice ID");
            var promptTemplate = RequireValue(request.PromptTemplate, "Prompt template");
            var pricingTier = NormalizePricingTier(request.PricingTier);
            if (request.IsClientSpecific && !request.ClientUserId.HasValue)
            {
                throw new InvalidOperationException("Client user id is required for client-specific voice packs.");
            }

            var duplicate = await Db.AiVoicePacks.AnyAsync(
                item => item.Id != id && item.Provider == provider && item.Name == name,
                cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("A voice pack with this name already exists for the provider.");
            }

            row.Provider = provider;
            row.Name = name;
            row.Accent = TrimOrNull(request.Accent);
            row.Language = TrimOrNull(request.Language);
            row.Tone = TrimOrNull(request.Tone);
            row.Persona = TrimOrNull(request.Persona);
            row.UseCasesJson = SerializeList(request.UseCases);
            row.VoiceId = voiceId;
            row.SampleAudioUrl = TrimOrNull(request.SampleAudioUrl);
            row.PromptTemplate = promptTemplate;
            row.PricingTier = pricingTier;
            row.IsClientSpecific = request.IsClientSpecific;
            row.ClientUserId = request.ClientUserId;
            row.IsClonedVoice = request.IsClonedVoice;
            row.AudienceTagsJson = SerializeList(request.AudienceTags);
            row.ObjectiveTagsJson = SerializeList(request.ObjectiveTags);
            row.IsActive = request.IsActive;
            row.SortOrder = request.SortOrder;
            row.UpdatedAt = DateTime.UtcNow;

            await Db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "ai_voice_pack",
                row.Id.ToString(),
                row.Name,
                $"Updated AI voice pack {row.Name}.",
                new { row.Provider, row.Name, row.PricingTier, row.IsClientSpecific, row.ClientUserId, row.IsClonedVoice, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoicePack(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("voice-packs/{id:guid}")]
    public async Task<IActionResult> DeleteVoicePack(Guid id, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var row = await Db.AiVoicePacks.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        Db.AiVoicePacks.Remove(row);
        await Db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "delete",
            "ai_voice_pack",
            id.ToString(),
            row.Name,
            $"Deleted AI voice pack {row.Name}.",
            new { row.Provider, row.Name },
            cancellationToken);
        return NoContent();
    }

    [HttpGet("voice-templates")]
    public async Task<ActionResult<IReadOnlyList<AdminAiVoiceTemplateResponse>>> GetVoiceTemplates(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var rows = await Db.AiVoicePromptTemplates
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.TemplateNumber)
            .ToArrayAsync(cancellationToken);

        return Ok(rows.Select(MapAdminAiVoiceTemplate).ToArray());
    }

    [HttpPost("voice-templates")]
    public async Task<ActionResult<AdminAiVoiceTemplateResponse>> CreateVoiceTemplate(
        [FromBody] UpsertAdminAiVoiceTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            if (request.TemplateNumber <= 0)
            {
                throw new InvalidOperationException("Template number must be greater than zero.");
            }

            var category = RequireValue(request.Category, "Category");
            var name = RequireValue(request.Name, "Name");
            var promptTemplate = RequireValue(request.PromptTemplate, "Prompt template");
            var primaryVoicePackName = RequireValue(request.PrimaryVoicePackName, "Primary voice pack name");

            var exists = await Db.AiVoicePromptTemplates.AnyAsync(
                item => item.TemplateNumber == request.TemplateNumber,
                cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("A template with this template number already exists.");
            }

            var now = DateTime.UtcNow;
            var row = new AiVoicePromptTemplate
            {
                Id = Guid.NewGuid(),
                TemplateNumber = request.TemplateNumber,
                Category = category,
                Name = name,
                PromptTemplate = promptTemplate,
                PrimaryVoicePackName = primaryVoicePackName,
                FallbackVoicePackNamesJson = SerializeList(request.FallbackVoicePackNames),
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                CreatedAt = now,
                UpdatedAt = now
            };

            Db.AiVoicePromptTemplates.Add(row);
            await Db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "create",
                "ai_voice_prompt_template",
                row.Id.ToString(),
                row.Name,
                $"Created AI voice template #{row.TemplateNumber} {row.Name}.",
                new { row.TemplateNumber, row.Category, row.Name, row.PrimaryVoicePackName, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoiceTemplate(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("voice-templates/{id:guid}")]
    public async Task<ActionResult<AdminAiVoiceTemplateResponse>> UpdateVoiceTemplate(
        Guid id,
        [FromBody] UpsertAdminAiVoiceTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var row = await Db.AiVoicePromptTemplates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (row is null)
            {
                return NotFound();
            }

            if (request.TemplateNumber <= 0)
            {
                throw new InvalidOperationException("Template number must be greater than zero.");
            }

            var category = RequireValue(request.Category, "Category");
            var name = RequireValue(request.Name, "Name");
            var promptTemplate = RequireValue(request.PromptTemplate, "Prompt template");
            var primaryVoicePackName = RequireValue(request.PrimaryVoicePackName, "Primary voice pack name");

            var duplicate = await Db.AiVoicePromptTemplates.AnyAsync(
                item => item.Id != id && item.TemplateNumber == request.TemplateNumber,
                cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("A template with this template number already exists.");
            }

            row.TemplateNumber = request.TemplateNumber;
            row.Category = category;
            row.Name = name;
            row.PromptTemplate = promptTemplate;
            row.PrimaryVoicePackName = primaryVoicePackName;
            row.FallbackVoicePackNamesJson = SerializeList(request.FallbackVoicePackNames);
            row.IsActive = request.IsActive;
            row.SortOrder = request.SortOrder;
            row.UpdatedAt = DateTime.UtcNow;

            await Db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "ai_voice_prompt_template",
                row.Id.ToString(),
                row.Name,
                $"Updated AI voice template #{row.TemplateNumber} {row.Name}.",
                new { row.TemplateNumber, row.Category, row.Name, row.PrimaryVoicePackName, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoiceTemplate(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("voice-templates/{id:guid}")]
    public async Task<IActionResult> DeleteVoiceTemplate(Guid id, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var row = await Db.AiVoicePromptTemplates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        Db.AiVoicePromptTemplates.Remove(row);
        await Db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "delete",
            "ai_voice_prompt_template",
            id.ToString(),
            row.Name,
            $"Deleted AI voice template #{row.TemplateNumber} {row.Name}.",
            new { row.TemplateNumber, row.Category, row.Name },
            cancellationToken);
        return NoContent();
    }
}
