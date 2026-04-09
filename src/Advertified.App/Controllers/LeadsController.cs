using Advertified.App.Contracts.Leads;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Advertified.App.Controllers;

[ApiController]
[Route("leads")]
public sealed class LeadsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILeadScoreService _leadScoreService;
    private readonly ILeadChannelDetectionService _leadChannelDetectionService;
    private readonly ILeadIndustryPolicyService _leadIndustryPolicyService;
    private readonly ILeadOpportunityProfileService _leadOpportunityProfileService;
    private readonly ILeadEnrichmentSnapshotService _leadEnrichmentSnapshotService;
    private readonly ILeadBusinessProfileService _leadBusinessProfileService;
    private readonly ILeadStrategyEngine _leadStrategyEngine;
    private readonly ILeadIntelligenceOrchestrator _leadIntelligenceOrchestrator;
    private readonly ILeadSourceIngestionService _leadSourceIngestionService;
    private readonly ILeadSourceImportService _leadSourceImportService;
    private readonly ILeadSourceDropFolderProcessor _leadSourceDropFolderProcessor;
    private readonly ILeadSourceAutomationStatusService _leadSourceAutomationStatusService;
    private readonly ILeadPaidMediaEvidenceSyncService _leadPaidMediaEvidenceSyncService;
    private readonly IWebsiteSignalProvider _websiteSignalProvider;
    private readonly ILeadMasterDataService _leadMasterDataService;
    private readonly IGeocodingService _geocodingService;
    private readonly LeadIntelligenceAutomationOptions _leadIntelligenceAutomationOptions;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public LeadsController(
        AppDbContext db,
        ILeadScoreService leadScoreService,
        ILeadChannelDetectionService leadChannelDetectionService,
        ILeadIndustryPolicyService leadIndustryPolicyService,
        ILeadOpportunityProfileService leadOpportunityProfileService,
        ILeadEnrichmentSnapshotService leadEnrichmentSnapshotService,
        ILeadBusinessProfileService leadBusinessProfileService,
        ILeadStrategyEngine leadStrategyEngine,
        ILeadIntelligenceOrchestrator leadIntelligenceOrchestrator,
        ILeadSourceIngestionService leadSourceIngestionService,
        ILeadSourceImportService leadSourceImportService,
        ILeadSourceDropFolderProcessor leadSourceDropFolderProcessor,
        ILeadSourceAutomationStatusService leadSourceAutomationStatusService,
        ILeadPaidMediaEvidenceSyncService leadPaidMediaEvidenceSyncService,
        IWebsiteSignalProvider websiteSignalProvider,
        ILeadMasterDataService leadMasterDataService,
        IGeocodingService geocodingService,
        IOptions<LeadIntelligenceAutomationOptions> leadIntelligenceAutomationOptions,
        ICurrentUserAccessor currentUserAccessor)
    {
        _db = db;
        _leadScoreService = leadScoreService;
        _leadChannelDetectionService = leadChannelDetectionService;
        _leadIndustryPolicyService = leadIndustryPolicyService;
        _leadOpportunityProfileService = leadOpportunityProfileService;
        _leadEnrichmentSnapshotService = leadEnrichmentSnapshotService;
        _leadBusinessProfileService = leadBusinessProfileService;
        _leadStrategyEngine = leadStrategyEngine;
        _leadIntelligenceOrchestrator = leadIntelligenceOrchestrator;
        _leadSourceIngestionService = leadSourceIngestionService;
        _leadSourceImportService = leadSourceImportService;
        _leadSourceDropFolderProcessor = leadSourceDropFolderProcessor;
        _leadSourceAutomationStatusService = leadSourceAutomationStatusService;
        _leadPaidMediaEvidenceSyncService = leadPaidMediaEvidenceSyncService;
        _websiteSignalProvider = websiteSignalProvider;
        _leadMasterDataService = leadMasterDataService;
        _geocodingService = geocodingService;
        _leadIntelligenceAutomationOptions = leadIntelligenceAutomationOptions.Value;
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
            var derivedContext = BuildDerivedContext(lead, signal, signalEvidence);

            results.Add(ComposeLeadIntelligenceDto(
                lead,
                signal,
                score,
                insight?.Text ?? (signal is null ? "No signal analysis has been run for this lead yet." : string.Empty),
                insight?.TrendSummary ?? string.Empty,
                derivedContext,
                signalHistory: Array.Empty<Signal>(),
                insightHistory: Array.Empty<LeadInsight>(),
                recommendedActions: action is null ? Array.Empty<LeadActionDto>() : new[] { action },
                interactionHistory: Array.Empty<LeadInteraction>()));
        }

        return Ok(results);
    }

    [HttpGet("source-automation/status")]
    public ActionResult<LeadSourceAutomationStatusDto> GetSourceAutomationStatus()
    {
        return Ok(_leadSourceAutomationStatusService.GetStatus());
    }

    [HttpGet("industry-policy/resolve")]
    public ActionResult<LeadIndustryPolicyDto> ResolveIndustryPolicy([FromQuery] string? category)
    {
        var policy = _leadIndustryPolicyService.ResolveForCategory(category);
        return Ok(ToDto(policy));
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

    [HttpGet("paid-media-sync/status")]
    public async Task<ActionResult<LeadPaidMediaSyncStatusDto>> GetPaidMediaSyncStatus(CancellationToken cancellationToken)
    {
        var latestRunAudit = await _db.ChangeAuditLogs
            .AsNoTracking()
            .Where(log => log.Scope == "system" && log.Action == "lead_paid_media_sync_run")
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new LeadPaidMediaSyncStatusDto
        {
            Enabled = _leadIntelligenceAutomationOptions.EnablePaidMediaEvidenceSync,
            BatchSize = Math.Max(1, _leadIntelligenceAutomationOptions.BatchSize),
            IntervalMinutes = Math.Max(15, _leadIntelligenceAutomationOptions.PaidMediaSyncIntervalMinutes),
            LastRun = TryParseSyncRunDto(latestRunAudit?.MetadataJson),
        });
    }

    [HttpPost("paid-media-sync/run-now")]
    public async Task<ActionResult<LeadPaidMediaSyncRunDto>> RunPaidMediaSyncNow(CancellationToken cancellationToken)
    {
        var result = await _leadPaidMediaEvidenceSyncService.SyncBatchAsync(cancellationToken);
        return Ok(ToDto(result));
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
        var derivedContext = BuildDerivedContext(lead, signal, signalEvidence);
        var latestInsight = insightHistory.FirstOrDefault();
        var insight = signal is null
            ? "No signal analysis has been run for this lead yet."
            : latestInsight?.Text ?? "No stored insight has been generated for this lead yet.";

        return Ok(ComposeLeadIntelligenceDto(
            lead,
            signal,
            score,
            insight,
            latestInsight?.TrendSummary ?? string.Empty,
            derivedContext,
            signalHistory,
            insightHistory,
            actionHistory,
            interactionHistory));
    }

    [HttpPost]
    public async Task<ActionResult<LeadDto>> Create([FromBody] CreateLeadRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(
                title: "Name is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var normalizedLocation = request.Location?.Trim() ?? string.Empty;
        var normalizedCategory = request.Category?.Trim() ?? string.Empty;
        var normalizedWebsite = request.Website?.Trim();
        var inferredFields = new List<string>();

        if ((string.IsNullOrWhiteSpace(normalizedLocation) || string.IsNullOrWhiteSpace(normalizedCategory))
            && !string.IsNullOrWhiteSpace(normalizedWebsite))
        {
            var inferred = await InferLeadInputFromWebsiteAsync(normalizedWebsite, cancellationToken);
            if (string.IsNullOrWhiteSpace(normalizedLocation) && !string.IsNullOrWhiteSpace(inferred.Location))
            {
                normalizedLocation = inferred.Location;
                inferredFields.Add("location");
            }

            if (string.IsNullOrWhiteSpace(normalizedCategory) && !string.IsNullOrWhiteSpace(inferred.Category))
            {
                normalizedCategory = inferred.Category;
                inferredFields.Add("category");
            }
        }

        if (string.IsNullOrWhiteSpace(normalizedLocation) || string.IsNullOrWhiteSpace(normalizedCategory))
        {
            return Problem(
                title: "Location and category are required, or provide a website so we can infer them.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var normalizedSourceReference = string.IsNullOrWhiteSpace(request.SourceReference)
            ? null
            : request.SourceReference.Trim();
        if (inferredFields.Count > 0)
        {
            var inferredMarker = $"auto_inferred:{string.Join(",", inferredFields.OrderBy(field => field, StringComparer.OrdinalIgnoreCase))};from:website_scan";
            normalizedSourceReference = string.IsNullOrWhiteSpace(normalizedSourceReference)
                ? inferredMarker
                : $"{normalizedSourceReference};{inferredMarker}";
        }

        var lead = new Lead
        {
            Name = request.Name.Trim(),
            Website = string.IsNullOrWhiteSpace(normalizedWebsite) ? null : normalizedWebsite,
            Location = normalizedLocation,
            Category = normalizedCategory,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source.Trim(),
            SourceReference = normalizedSourceReference,
            LastDiscoveredAt = DateTime.UtcNow,
        };

        var geocodedLeadLocation = _geocodingService.ResolveLocation(lead.Location);
        if (geocodedLeadLocation.IsResolved)
        {
            lead.Location = geocodedLeadLocation.CanonicalLocation;
            lead.Latitude = geocodedLeadLocation.Latitude;
            lead.Longitude = geocodedLeadLocation.Longitude;
        }

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
        var derivedContext = BuildDerivedContext(lead, result.Signal, resultSignalEvidence);

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

        return Ok(ComposeLeadIntelligenceDto(
            lead,
            result.Signal,
            result.Score,
            result.Insight.Text,
            result.Insight.TrendSummary,
            derivedContext,
            signalHistory,
            insightHistory,
            actionHistory,
            interactionHistory));
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

    private LeadDerivedContext BuildDerivedContext(
        Lead lead,
        Signal? signal,
        IReadOnlyList<LeadSignalEvidence> signalEvidence)
    {
        var channelDetections = _leadChannelDetectionService.Detect(lead, signal, signalEvidence);
        var industryPolicy = _leadIndustryPolicyService.ResolveForCategory(lead.Category);
        var opportunityProfile = _leadOpportunityProfileService.Build(lead, signal, channelDetections, industryPolicy);
        var enrichment = _leadEnrichmentSnapshotService.Build(lead, signal, signalEvidence, channelDetections);
        var businessProfile = _leadBusinessProfileService.Build(lead, enrichment, industryPolicy, opportunityProfile);
        var strategy = _leadStrategyEngine.Build(businessProfile, industryPolicy, opportunityProfile, channelDetections);

        return new LeadDerivedContext(channelDetections, industryPolicy, opportunityProfile, enrichment, businessProfile, strategy);
    }

    private static LeadIntelligenceDto ComposeLeadIntelligenceDto(
        Lead lead,
        Signal? signal,
        LeadScoreResult score,
        string insight,
        string trendSummary,
        LeadDerivedContext context,
        IReadOnlyList<Signal> signalHistory,
        IReadOnlyList<LeadInsight> insightHistory,
        IReadOnlyList<LeadActionDto> recommendedActions,
        IReadOnlyList<LeadInteraction> interactionHistory)
    {
        return new LeadIntelligenceDto
        {
            Lead = ToDto(lead),
            LatestSignal = signal is null ? null : ToDto(signal),
            Score = ToDto(score),
            Insight = insight,
            TrendSummary = trendSummary,
            ChannelDetections = context.ChannelDetections.Select(ToDto).ToList(),
            SignalHistory = signalHistory.Select(ToDto).ToList(),
            InsightHistory = insightHistory.Select(ToDto).ToList(),
            RecommendedActions = recommendedActions,
            InteractionHistory = interactionHistory.Select(ToDto).ToList(),
            IndustryPolicy = ToDto(context.IndustryPolicy),
            OpportunityProfile = ToDto(context.OpportunityProfile),
            Enrichment = ToDto(context.Enrichment),
            BusinessProfile = ToDto(context.BusinessProfile),
            Strategy = ToDto(context.Strategy),
        };
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
            AutoInferredFields = ParseAutoInferredFields(lead.SourceReference),
            LastDiscoveredAt = lead.LastDiscoveredAt,
            Latitude = lead.Latitude,
            Longitude = lead.Longitude,
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

    private static LeadIndustryPolicyDto ToDto(LeadIndustryPolicyProfile profile)
    {
        return new LeadIndustryPolicyDto
        {
            Key = profile.Key,
            Name = profile.Name,
            ObjectiveOverride = profile.ObjectiveOverride,
            PreferredTone = profile.PreferredTone,
            PreferredChannels = profile.PreferredChannels,
            Cta = profile.Cta,
            MessagingAngle = profile.MessagingAngle,
            Guardrails = profile.Guardrails,
            AdditionalGap = profile.AdditionalGap,
            AdditionalOutcome = profile.AdditionalOutcome,
        };
    }

    private static LeadOpportunityProfileDto ToDto(LeadOpportunityProfile profile)
    {
        return new LeadOpportunityProfileDto
        {
            Key = profile.Key,
            Name = profile.Name,
            SuggestedCampaignType = profile.SuggestedCampaignType,
            DetectedGaps = profile.DetectedGaps,
            ExpectedOutcome = profile.ExpectedOutcome,
            RecommendedChannels = profile.RecommendedChannels,
            WhyActNow = profile.WhyActNow,
        };
    }

    private static LeadPaidMediaSyncRunDto ToDto(LeadPaidMediaSyncRunResult result)
    {
        return new LeadPaidMediaSyncRunDto
        {
            StartedAtUtc = result.StartedAtUtc,
            FinishedAtUtc = result.FinishedAtUtc,
            Skipped = result.Skipped,
            SkipReason = result.SkipReason,
            TotalLeadCount = result.TotalLeadCount,
            ProcessedLeadCount = result.ProcessedLeadCount,
            FailedLeadCount = result.FailedLeadCount,
            EvidenceRowCount = result.EvidenceRowCount,
            EnabledProviders = result.EnabledProviders,
            ProviderEvidenceCounts = result.ProviderEvidenceCounts,
        };
    }

    private static LeadPaidMediaSyncRunDto? TryParseSyncRunDto(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<LeadPaidMediaSyncRunResult>(metadataJson);
            if (payload is null)
            {
                return null;
            }

            return ToDto(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static LeadEnrichmentSnapshotDto ToDto(LeadEnrichmentSnapshot snapshot)
    {
        return new LeadEnrichmentSnapshotDto
        {
            Fields = snapshot.Fields.Select(field => new LeadEnrichmentFieldDto
            {
                Key = field.Key,
                Label = field.Label,
                Value = field.Value,
                Confidence = field.Confidence,
                Source = field.Source,
                Reason = field.Reason,
                Required = field.Required
            }).ToList(),
            ConfidenceGate = new LeadConfidenceGateDto
            {
                IsBlocked = snapshot.ConfidenceGate.IsBlocked,
                RequiredFields = snapshot.ConfidenceGate.RequiredFields,
                MissingRequiredFields = snapshot.ConfidenceGate.MissingRequiredFields,
                Message = snapshot.ConfidenceGate.Message
            },
            ConfidenceScore = snapshot.ConfidenceScore,
            MissingFields = snapshot.MissingFields,
            GeneratedAtUtc = snapshot.GeneratedAtUtc
        };
    }

    private static LeadBusinessProfileDto ToDto(LeadBusinessProfile profile)
    {
        return new LeadBusinessProfileDto
        {
            BusinessType = profile.BusinessType,
            PrimaryLocation = profile.PrimaryLocation,
            TargetAudience = profile.TargetAudience,
            GenderFocus = profile.GenderFocus,
            Languages = profile.Languages,
            ConfidenceScore = profile.ConfidenceScore,
            MissingFields = profile.MissingFields,
            EvidenceTrace = profile.EvidenceTrace.Select(trace => new LeadEvidenceFieldTraceDto
            {
                Field = trace.Field,
                Value = trace.Value,
                Confidence = trace.Confidence,
                Source = trace.Source,
                Reason = trace.Reason,
            }).ToArray()
        };
    }

    private static LeadStrategyDto ToDto(LeadStrategyResult strategy)
    {
        return new LeadStrategyDto
        {
            Archetype = strategy.Archetype,
            Objective = strategy.Objective,
            Channels = strategy.Channels.Select(channel => new LeadStrategyChannelDto
            {
                Channel = channel.Channel,
                BudgetSharePercent = channel.BudgetSharePercent,
                Reason = channel.Reason
            }).ToArray(),
            GeoTargets = strategy.GeoTargets,
            Timing = strategy.Timing,
            Rationale = strategy.Rationale,
        };
    }

    private sealed record LeadDerivedContext(
        IReadOnlyList<LeadChannelDetectionResult> ChannelDetections,
        LeadIndustryPolicyProfile IndustryPolicy,
        LeadOpportunityProfile OpportunityProfile,
        LeadEnrichmentSnapshot Enrichment,
        LeadBusinessProfile BusinessProfile,
        LeadStrategyResult Strategy);

    private async Task<LeadInputInferenceResult> InferLeadInputFromWebsiteAsync(string websiteUrl, CancellationToken cancellationToken)
    {
        var signal = await _websiteSignalProvider.CollectAsync(websiteUrl, cancellationToken);
        var location = signal.LocationHints
            .Select(hint => _leadMasterDataService.ResolveLocation(hint))
            .FirstOrDefault(match => match is not null)?.CanonicalName
            ?? InferLocationFromHints(signal.LocationHints);
        var category = signal.IndustryHints
            .Select(hint => _leadMasterDataService.ResolveIndustry(hint))
            .FirstOrDefault(match => match is not null)?.Label
            ?? InferCategoryFromHints(signal.IndustryHints);
        return new LeadInputInferenceResult(location, category);
    }

    private static string? InferLocationFromHints(IReadOnlyList<string> hints)
    {
        if (hints.Count == 0)
        {
            return null;
        }

        var ordered = new[]
        {
            "johannesburg",
            "pretoria",
            "cape town",
            "durban",
            "gauteng",
            "western cape",
            "kwazulu-natal",
            "south africa",
        };
        var matched = ordered.FirstOrDefault(candidate => hints.Any(hint => hint.Equals(candidate, StringComparison.OrdinalIgnoreCase)));
        return matched is null
            ? ToTitleCase(hints[0])
            : ToTitleCase(matched);
    }

    private static string? InferCategoryFromHints(IReadOnlyList<string> hints)
    {
        if (hints.Count == 0)
        {
            return null;
        }

        if (ContainsAnyHint(hints, "funeral", "memorial", "burial", "cremation"))
        {
            return "Funeral Services";
        }

        if (ContainsAnyHint(hints, "dental", "clinic", "medical"))
        {
            return "Healthcare";
        }

        if (ContainsAnyHint(hints, "legal", "attorney"))
        {
            return "Legal Services";
        }

        if (ContainsAnyHint(hints, "retail", "grocery", "shop", "supermarket"))
        {
            return "Retail";
        }

        if (ContainsAnyHint(hints, "fitness", "gym"))
        {
            return "Fitness";
        }

        if (ContainsAnyHint(hints, "restaurant", "food", "cafe"))
        {
            return "Food & Hospitality";
        }

        return ToTitleCase(hints[0]);
    }

    private static bool ContainsAnyHint(IReadOnlyList<string> hints, params string[] tokens)
    {
        return hints.Any(hint => tokens.Any(token => hint.Equals(token, StringComparison.OrdinalIgnoreCase)));
    }

    private static string ToTitleCase(string value)
    {
        var parts = value
            .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant());
        return string.Join(" ", parts);
    }

    private static IReadOnlyList<string> ParseAutoInferredFields(string? sourceReference)
    {
        if (string.IsNullOrWhiteSpace(sourceReference))
        {
            return Array.Empty<string>();
        }

        var markerSegment = sourceReference
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(segment => segment.StartsWith("auto_inferred:", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(markerSegment))
        {
            return Array.Empty<string>();
        }

        return markerSegment["auto_inferred:".Length..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(field => field.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record LeadInputInferenceResult(string? Location, string? Category);
}
