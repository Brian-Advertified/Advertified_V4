using System.Text.Json;
using Advertified.App.AIPlatform.Api;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("api/v2/ai-platform/voice-packs")]
public sealed class AiVoicePacksController : ControllerBase
{
    private readonly AppDbContext _db;

    public AiVoicePacksController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VoicePackResponse>>> Get(
        [FromQuery] string? provider,
        CancellationToken cancellationToken)
    {
        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "ElevenLabs" : provider.Trim();
        var rows = await _db.AiVoicePacks
            .AsNoTracking()
            .Where(item => item.Provider == normalizedProvider && item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToArrayAsync(cancellationToken);

        return Ok(rows.Select(Map).ToArray());
    }

    private static VoicePackResponse Map(AiVoicePack row)
    {
        return new VoicePackResponse
        {
            Id = row.Id,
            Provider = row.Provider,
            Name = row.Name,
            Accent = row.Accent,
            Language = row.Language,
            Tone = row.Tone,
            Persona = row.Persona,
            UseCases = DeserializeList(row.UseCasesJson),
            SampleAudioUrl = row.SampleAudioUrl,
            PromptTemplate = row.PromptTemplate,
            PricingTier = row.PricingTier,
            SortOrder = row.SortOrder
        };
    }

    private static string[] DeserializeList(string? json)
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
}
