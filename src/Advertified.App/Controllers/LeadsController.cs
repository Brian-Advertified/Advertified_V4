using Advertified.App.Contracts.Leads;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("leads")]
public sealed class LeadsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILeadScoreService _leadScoreService;
    private readonly ILeadChannelDetectionService _leadChannelDetectionService;
    private readonly ILeadIntelligenceOrchestrator _leadIntelligenceOrchestrator;
    private readonly ILeadSourceIngestionService _leadSourceIngestionService;
    private readonly ILeadSourceImportService _leadSourceImportService;
    private readonly ILeadSourceDropFolderProcessor _leadSourceDropFolderProcessor;
    private readonly ILeadSourceAutomationStatusService _leadSourceAutomationStatusService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public LeadsController(
        AppDbContext db,
        ILeadScoreService leadScoreService,
        ILeadChannelDetectionService leadChannelDetectionService,
        ILeadIntelligenceOrchestrator leadIntelligenceOrchestrator,
        ILeadSourceIngestionService leadSourceIngestionService,
        ILeadSourceImportService leadSourceImportService,
        ILeadSourceDropFolderProcessor leadSourceDropFolderProcessor,
        ILeadSourceAutomationStatusService leadSourceAutomationStatusService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _db = db;
        _leadScoreService = leadScoreService;
        _leadChannelDetectionService = leadChannelDetectionService;
        _leadIntelligenceOrchestrator = leadIntelligenceOrchestrator;
        _leadSourceIngestionService = leadSourceIngestionService;
        _leadSourceImportService = leadSourceImportService;
        _leadSourceDropFolderProcessor = leadSourceDropFolderProcessor;
        _leadSourceAutomationStatusService = leadSourceAutomationStatusService;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeadDto>>> GetAll(CancellationToken cancellationToken)
    {
        var leads = await _db.Leads
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return Ok(leads);
    }

    [HttpGet("intelligence")]
    public async Task<ActionResult<IReadOnlyList<LeadIntelligenceDto>>> GetIntelligenceList(CancellationToken cancellationToken)
    {
        var leads = await _db.Leads
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var latestSignals = await _db.Signals
            .AsNoTracking()
            .GroupBy(x => x.LeadId)
            .Select(group => group
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .First())
            .ToDictionaryAsync(x => x.LeadId, cancellationToken);
        var latestSignalIds = latestSignals.Values.Select(signal => signal.Id).Distinct().ToArray();
        var latestSignalEvidenceBySignalId = latestSignalIds.Length == 0
            ? new Dictionary<int, List<LeadSignalEvidence>>()
            : await _db.LeadSignalEvidences
                .AsNoTracking()
                .Where(item => latestSignalIds.Contains(item.SignalId))
                .OrderByDescending(item => item.CreatedAt)
                .GroupBy(item => item.SignalId)
                .ToDictionaryAsync(group => group.Key, group => group.ToList(), cancellationToken);

        var latestInsights = await _db.LeadInsights
            .AsNoTracking()
            .GroupBy(x => x.LeadId)
            .Select(group => group
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .First())
            .ToDictionaryAsync(x => x.LeadId, cancellationToken);

        var recommendedActions = await QueryLeadActionDtos()
            .Where(x => x.Status == "open")
            .GroupBy(x => x.LeadId)
            .Select(group => group
                .OrderByDescending(x => x.Priority == "high")
                .ThenByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .First())
            .ToDictionaryAsync(x => x.LeadId, cancellationToken);

        var results = new List<LeadIntelligenceDto>(leads.Count);
        foreach (var lead in leads)
        {
            latestSignals.TryGetValue(lead.Id, out var signal);
            IReadOnlyList<LeadSignalEvidence> signalEvidence;
            if (signal is null)
            {
                signalEvidence = Array.Empty<LeadSignalEvidence>();
            }
            else
            {
                signalEvidence = latestSignalEvidenceBySignalId.TryGetValue(signal.Id, out var rows)
                    ? rows
                    : Array.Empty<LeadSignalEvidence>();
            }
            latestInsights.TryGetValue(lead.Id, out var insight);
            recommendedActions.TryGetValue(lead.Id, out var action);
            var score = await _leadScoreService.ScoreAsync(lead.Id, cancellationToken);
            var channelDetections = _leadChannelDetectionService.Detect(lead, signal, signalEvidence);

            results.Add(new LeadIntelligenceDto
            {
                Lead = ToDto(lead),
                LatestSignal = signal is null ? null : ToDto(signal),
                Score = ToDto(score),
                Insight = insight?.Text ?? (signal is null
                    ? "No signal analysis has been run for this lead yet."
                    : string.Empty),
                TrendSummary = insight?.TrendSummary ?? string.Empty,
                ChannelDetections = channelDetections.Select(ToDto).ToList(),
                RecommendedActions = action is null ? Array.Empty<LeadActionDto>() : new[] { action },
            });
        }

        return Ok(results);
    }

    [HttpGet("source-automation/status")]
    public ActionResult<LeadSourceAutomationStatusDto> GetSourceAutomationStatus()
    {
        return Ok(_leadSourceAutomationStatusService.GetStatus());
    }

    [HttpPost("source-automation/process-now")]
    public async Task<ActionResult<LeadSourceAutomationRunDto>> ProcessSourceAutomationNow(CancellationToken cancellationToken)
    {
        var result = await _leadSourceDropFolderProcessor.ProcessAsync(cancellationToken);

        return Ok(new LeadSourceAutomationRunDto
        {
            ProcessedFileCount = result.ProcessedFileCount,
            FailedFileCount = result.FailedFileCount,
            ImportedLeadCount = result.ImportedLeadCount,
            AnalyzedLeadCount = result.AnalyzedLeadCount,
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeadDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var lead = await _db.Leads
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => ToDto(x))
            .FirstOrDefaultAsync(cancellationToken);

        if (lead is null)
        {
            return NotFound();
        }

        return Ok(lead);
    }

    [HttpGet("{id:int}/intelligence")]
    public async Task<ActionResult<LeadIntelligenceDto>> GetIntelligence(int id, CancellationToken cancellationToken)
    {
        var lead = await _db.Leads
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (lead is null)
        {
            return NotFound();
        }

        var signal = await _db.Signals
            .AsNoTracking()
            .Where(x => x.LeadId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var signalHistory = await _db.Signals
            .AsNoTracking()
            .Where(x => x.LeadId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        var insightHistory = await _db.LeadInsights
            .AsNoTracking()
            .Where(x => x.LeadId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        var actionHistory = await QueryLeadActionDtos()
            .Where(x => x.LeadId == id)
            .OrderBy(x => x.Status == "open" ? 0 : 1)
            .ThenByDescending(x => x.Priority == "high")
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        var interactionHistory = await _db.LeadInteractions
            .AsNoTracking()
            .Where(x => x.LeadId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        var score = await _leadScoreService.ScoreAsync(id, cancellationToken);
        var signalEvidence = signal is null
            ? Array.Empty<LeadSignalEvidence>()
            : await _db.LeadSignalEvidences
                .AsNoTracking()
                .Where(item => item.SignalId == signal.Id)
                .OrderByDescending(item => item.CreatedAt)
                .ToArrayAsync(cancellationToken);
        var channelDetections = _leadChannelDetectionService.Detect(lead, signal, signalEvidence);
        var latestInsight = insightHistory.FirstOrDefault();
        var insight = signal is null
            ? "No signal analysis has been run for this lead yet."
            : latestInsight?.Text ?? "No stored insight has been generated for this lead yet.";

        return Ok(new LeadIntelligenceDto
        {
            Lead = ToDto(lead),
            LatestSignal = signal is null ? null : ToDto(signal),
            Score = ToDto(score),
            Insight = insight,
            TrendSummary = latestInsight?.TrendSummary ?? string.Empty,
            ChannelDetections = channelDetections.Select(ToDto).ToList(),
            SignalHistory = signalHistory.Select(ToDto).ToList(),
            InsightHistory = insightHistory.Select(ToDto).ToList(),
            RecommendedActions = actionHistory,
            InteractionHistory = interactionHistory.Select(ToDto).ToList(),
        });
    }

    [HttpPost]
    public async Task<ActionResult<LeadDto>> Create([FromBody] CreateLeadRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Location) ||
            string.IsNullOrWhiteSpace(request.Category))
        {
            return Problem(
                title: "Name, location, and category are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var lead = new Lead
        {
            Name = request.Name.Trim(),
            Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim(),
            Location = request.Location.Trim(),
            Category = request.Category.Trim(),
            Source = string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source.Trim(),
            SourceReference = string.IsNullOrWhiteSpace(request.SourceReference) ? null : request.SourceReference.Trim(),
            LastDiscoveredAt = DateTime.UtcNow,
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = ToDto(lead);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPost("ingest-source-batch")]
    public async Task<ActionResult<LeadSourceIngestionResultDto>> IngestSourceBatch(
        [FromBody] IngestLeadSourceBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Leads.Count == 0)
        {
            return Problem(
                title: "At least one lead is required for source ingestion.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await _leadSourceIngestionService.IngestAsync(request.Leads, cancellationToken);

        return Ok(new LeadSourceIngestionResultDto
        {
            CreatedCount = result.CreatedCount,
            UpdatedCount = result.UpdatedCount,
            Leads = result.Leads.Select(ToDto).ToList(),
        });
    }

    [HttpPost("import-csv")]
    public async Task<ActionResult<LeadSourceIngestionResultDto>> ImportCsv(
        [FromBody] ImportLeadCsvRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CsvText))
        {
            return Problem(
                title: "CSV text is required for import.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await _leadSourceImportService.ImportCsvAsync(
            request.CsvText,
            string.IsNullOrWhiteSpace(request.DefaultSource) ? "csv_import" : request.DefaultSource.Trim(),
            string.IsNullOrWhiteSpace(request.ImportProfile) ? "standard" : request.ImportProfile.Trim(),
            cancellationToken);

        return Ok(new LeadSourceIngestionResultDto
        {
            CreatedCount = result.CreatedCount,
            UpdatedCount = result.UpdatedCount,
            Leads = result.Leads.Select(ToDto).ToList(),
        });
    }

    [HttpPost("{id:int}/analyze")]
    public async Task<ActionResult<LeadIntelligenceDto>> Analyze(int id, CancellationToken cancellationToken)
    {
        var lead = await _db.Leads
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (lead is null)
        {
            return NotFound();
        }

        var result = await _leadIntelligenceOrchestrator.RunLeadAsync(id, cancellationToken);
        var resultSignalEvidence = await _db.LeadSignalEvidences
            .AsNoTracking()
            .Where(item => item.SignalId == result.Signal.Id)
            .OrderByDescending(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var channelDetections = _leadChannelDetectionService.Detect(lead, result.Signal, resultSignalEvidence);

        var signalHistory = await _db.Signals
            .AsNoTracking()
            .Where(x => x.LeadId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        var insightHistory = await _db.LeadInsights
            .AsNoTracking()
            .Where(x => x.LeadId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        var actionHistory = await QueryLeadActionDtos()
            .Where(x => x.LeadId == id)
            .OrderBy(x => x.Status == "open" ? 0 : 1)
            .ThenByDescending(x => x.Priority == "high")
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        var interactionHistory = await _db.LeadInteractions
            .AsNoTracking()
            .Where(x => x.LeadId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        return Ok(new LeadIntelligenceDto
        {
            Lead = ToDto(lead),
            LatestSignal = ToDto(result.Signal),
            Score = ToDto(result.Score),
            Insight = result.Insight.Text,
            TrendSummary = result.Insight.TrendSummary,
            ChannelDetections = channelDetections.Select(ToDto).ToList(),
            SignalHistory = signalHistory.Select(ToDto).ToList(),
            InsightHistory = insightHistory.Select(ToDto).ToList(),
            RecommendedActions = actionHistory,
            InteractionHistory = interactionHistory.Select(ToDto).ToList(),
        });
    }

    [HttpPost("{leadId:int}/actions/{actionId:int}/status")]
    public async Task<ActionResult<LeadActionDto>> UpdateActionStatus(
        int leadId,
        int actionId,
        [FromBody] UpdateLeadActionStatusRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = request.Status.Trim().ToLowerInvariant();
        if (normalizedStatus is not ("open" or "completed" or "dismissed"))
        {
            return Problem(
                title: "Status must be open, completed, or dismissed.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var action = await _db.LeadActions
            .FirstOrDefaultAsync(x => x.Id == actionId && x.LeadId == leadId, cancellationToken);

        if (action is null)
        {
            return NotFound();
        }

        action.Status = normalizedStatus;
        action.CompletedAt = normalizedStatus == "completed" ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(action));
    }

    [HttpPost("{leadId:int}/actions/{actionId:int}/assign-to-me")]
    public async Task<ActionResult<LeadActionDto>> AssignActionToMe(
        int leadId,
        int actionId,
        CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var action = await _db.LeadActions
            .Include(x => x.AssignedAgentUser)
            .FirstOrDefaultAsync(x => x.Id == actionId && x.LeadId == leadId, cancellationToken);

        if (action is null)
        {
            return NotFound();
        }

        action.AssignedAgentUserId = currentUserId;
        action.AssignedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _db.Entry(action)
            .Reference(x => x.AssignedAgentUser)
            .LoadAsync(cancellationToken);

        return Ok(ToDto(action, currentUserId));
    }

    [HttpPost("{leadId:int}/actions/{actionId:int}/unassign")]
    public async Task<ActionResult<LeadActionDto>> UnassignAction(
        int leadId,
        int actionId,
        CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var action = await _db.LeadActions
            .Include(x => x.AssignedAgentUser)
            .FirstOrDefaultAsync(x => x.Id == actionId && x.LeadId == leadId, cancellationToken);

        if (action is null)
        {
            return NotFound();
        }

        if (action.AssignedAgentUserId.HasValue && action.AssignedAgentUserId != currentUserId)
        {
            return Problem(
                title: "Only the assigned agent can remove this assignment.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        action.AssignedAgentUserId = null;
        action.AssignedAt = null;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(action, currentUserId));
    }

    [HttpPost("{leadId:int}/interactions")]
    public async Task<ActionResult<LeadInteractionDto>> CreateInteraction(
        int leadId,
        [FromBody] CreateLeadInteractionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InteractionType) || string.IsNullOrWhiteSpace(request.Notes))
        {
            return Problem(
                title: "Interaction type and notes are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var leadExists = await _db.Leads.AnyAsync(x => x.Id == leadId, cancellationToken);
        if (!leadExists)
        {
            return NotFound();
        }

        if (request.LeadActionId.HasValue)
        {
            var actionExists = await _db.LeadActions.AnyAsync(
                x => x.Id == request.LeadActionId.Value && x.LeadId == leadId,
                cancellationToken);

            if (!actionExists)
            {
                return Problem(
                    title: "The specified lead action does not belong to this lead.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        var interaction = new LeadInteraction
        {
            LeadId = leadId,
            LeadActionId = request.LeadActionId,
            InteractionType = request.InteractionType.Trim(),
            Notes = request.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        _db.LeadInteractions.Add(interaction);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(interaction));
    }

    private static LeadDto ToDto(Lead lead)
    {
        return new LeadDto
        {
            Id = lead.Id,
            Name = lead.Name,
            Website = lead.Website,
            Location = lead.Location,
            Category = lead.Category,
            Source = lead.Source,
            SourceReference = lead.SourceReference,
            LastDiscoveredAt = lead.LastDiscoveredAt,
            CreatedAt = lead.CreatedAt
        };
    }

    private static SignalDto ToDto(Signal signal)
    {
        return new SignalDto
        {
            Id = signal.Id,
            LeadId = signal.LeadId,
            HasPromo = signal.HasPromo,
            HasMetaAds = signal.HasMetaAds,
            WebsiteUpdatedRecently = signal.WebsiteUpdatedRecently,
            CreatedAt = signal.CreatedAt,
        };
    }

    private static LeadScoreDto ToDto(LeadScoreResult score)
    {
        return new LeadScoreDto
        {
            LeadId = score.LeadId,
            Score = score.Score,
            IntentLevel = score.IntentLevel,
        };
    }

    private static LeadInsightDto ToDto(LeadInsight insight)
    {
        return new LeadInsightDto
        {
            Id = insight.Id,
            LeadId = insight.LeadId,
            SignalId = insight.SignalId,
            TrendSummary = insight.TrendSummary,
            ScoreSnapshot = insight.ScoreSnapshot,
            IntentLevelSnapshot = insight.IntentLevelSnapshot,
            Text = insight.Text,
            CreatedAt = insight.CreatedAt,
        };
    }

    private IQueryable<LeadActionDto> QueryLeadActionDtos(Guid? currentUserId = null)
    {
        return _db.LeadActions
            .AsNoTracking()
            .Select(action => new LeadActionDto
            {
                Id = action.Id,
                LeadId = action.LeadId,
                LeadInsightId = action.LeadInsightId,
                ActionType = action.ActionType,
                Title = action.Title,
                Description = action.Description,
                Status = action.Status,
                Priority = action.Priority,
                AssignedAgentUserId = action.AssignedAgentUserId,
                AssignedAgentName = action.AssignedAgentUser != null ? action.AssignedAgentUser.FullName : null,
                AssignedAt = action.AssignedAt,
                IsAssignedToCurrentUser = currentUserId.HasValue && action.AssignedAgentUserId == currentUserId.Value,
                IsUnassigned = action.AssignedAgentUserId == null,
                CreatedAt = action.CreatedAt,
                CompletedAt = action.CompletedAt,
            });
    }

    private static LeadActionDto ToDto(LeadAction action, Guid? currentUserId = null)
    {
        return new LeadActionDto
        {
            Id = action.Id,
            LeadId = action.LeadId,
            LeadInsightId = action.LeadInsightId,
            ActionType = action.ActionType,
            Title = action.Title,
            Description = action.Description,
            Status = action.Status,
            Priority = action.Priority,
            AssignedAgentUserId = action.AssignedAgentUserId,
            AssignedAgentName = action.AssignedAgentUser?.FullName,
            AssignedAt = action.AssignedAt,
            IsAssignedToCurrentUser = currentUserId.HasValue && action.AssignedAgentUserId == currentUserId.Value,
            IsUnassigned = action.AssignedAgentUserId == null,
            CreatedAt = action.CreatedAt,
            CompletedAt = action.CompletedAt,
        };
    }

    private static LeadInteractionDto ToDto(LeadInteraction interaction)
    {
        return new LeadInteractionDto
        {
            Id = interaction.Id,
            LeadId = interaction.LeadId,
            LeadActionId = interaction.LeadActionId,
            InteractionType = interaction.InteractionType,
            Notes = interaction.Notes,
            CreatedAt = interaction.CreatedAt,
        };
    }

    private static LeadChannelDetectionDto ToDto(LeadChannelDetectionResult result)
    {
        return new LeadChannelDetectionDto
        {
            LeadId = result.LeadId,
            Channel = result.Channel,
            Score = result.Score,
            Confidence = result.Confidence,
            Status = result.Status,
            DominantReason = result.DominantReason,
            LastEvidenceAtUtc = result.LastEvidenceAtUtc,
            Signals = result.Signals.Select(signal => new LeadChannelSignalDto
            {
                Type = signal.Type,
                Source = signal.Source,
                Weight = signal.Weight,
                ReliabilityMultiplier = signal.ReliabilityMultiplier,
                FreshnessMultiplier = signal.FreshnessMultiplier,
                EffectiveWeight = (int)Math.Round(signal.EffectiveWeight, MidpointRounding.AwayFromZero),
                Value = signal.Value
            }).ToList()
        };
    }
}
