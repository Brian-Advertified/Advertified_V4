using System.Net.Http.Json;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Leads;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class GoogleSheetsLeadIntegrationService : IGoogleSheetsLeadIntegrationService
{
    private readonly HttpClient _httpClient;
    private readonly GoogleSheetsLeadOpsOptions _options;
    private readonly ILeadSourceImportService _leadSourceImportService;
    private readonly ILeadOpsInboxService _leadOpsInboxService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly ILogger<GoogleSheetsLeadIntegrationService> _logger;

    public GoogleSheetsLeadIntegrationService(
        HttpClient httpClient,
        IOptions<GoogleSheetsLeadOpsOptions> options,
        ILeadSourceImportService leadSourceImportService,
        ILeadOpsInboxService leadOpsInboxService,
        IChangeAuditService changeAuditService,
        ILogger<GoogleSheetsLeadIntegrationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _leadSourceImportService = leadSourceImportService;
        _leadOpsInboxService = leadOpsInboxService;
        _changeAuditService = changeAuditService;
        _logger = logger;
    }

    public GoogleSheetsLeadIntegrationStatusDto GetStatus()
    {
        var sources = _options.IntakeSources.Select(source => new GoogleSheetsLeadSourceStatusDto
        {
            Name = source.Name,
            Enabled = source.Enabled,
            DefaultSource = source.DefaultSource,
            ImportProfile = source.ImportProfile,
            CsvExportUrl = source.CsvExportUrl
        }).ToArray();

        return new GoogleSheetsLeadIntegrationStatusDto
        {
            Enabled = _options.Enabled,
            ImportEnabled = _options.Enabled && _options.ImportEnabled,
            ExportEnabled = _options.Enabled && _options.ExportEnabled,
            ImportPollIntervalMinutes = Math.Max(1, _options.ImportPollIntervalMinutes),
            ExportPollIntervalMinutes = Math.Max(1, _options.ExportPollIntervalMinutes),
            ExportWebhookConfigured = !string.IsNullOrWhiteSpace(_options.ExportWebhookUrl),
            ConfiguredSourceCount = sources.Length,
            ActiveSourceCount = sources.Count(x => x.Enabled && !string.IsNullOrWhiteSpace(x.CsvExportUrl)),
            Sources = sources
        };
    }

    public async Task<GoogleSheetsLeadIntegrationRunDto> ImportConfiguredSourcesAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.ImportEnabled)
        {
            return new GoogleSheetsLeadIntegrationRunDto
            {
                Operation = "import",
                Message = "Google Sheets import is disabled."
            };
        }

        var processedSourceCount = 0;
        var failedSourceCount = 0;
        var createdLeadCount = 0;
        var updatedLeadCount = 0;

        foreach (var source in _options.IntakeSources.Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.CsvExportUrl)))
        {
            try
            {
                var csvText = await _httpClient.GetStringAsync(source.CsvExportUrl.Trim(), cancellationToken);
                var result = await _leadSourceImportService.ImportCsvAsync(
                    csvText,
                    string.IsNullOrWhiteSpace(source.DefaultSource) ? "google_sheet" : source.DefaultSource.Trim(),
                    string.IsNullOrWhiteSpace(source.ImportProfile) ? "standard" : source.ImportProfile.Trim(),
                    cancellationToken);

                processedSourceCount++;
                createdLeadCount += result.CreatedCount;
                updatedLeadCount += result.UpdatedCount;
            }
            catch (Exception ex)
            {
                failedSourceCount++;
                _logger.LogError(ex, "Google Sheets lead import failed for source {SourceName}.", source.Name);
            }
        }

        await _changeAuditService.WriteAsync(
            null,
            "system",
            "google_sheets_import_run",
            "lead_source",
            "google_sheets",
            "Google Sheets lead intake",
            $"Imported lead sources from Google Sheets. Processed {processedSourceCount}, failed {failedSourceCount}, created {createdLeadCount}, updated {updatedLeadCount}.",
            new
            {
                ProcessedSourceCount = processedSourceCount,
                FailedSourceCount = failedSourceCount,
                CreatedLeadCount = createdLeadCount,
                UpdatedLeadCount = updatedLeadCount
            },
            cancellationToken);

        return new GoogleSheetsLeadIntegrationRunDto
        {
            Operation = "import",
            ProcessedSourceCount = processedSourceCount,
            FailedSourceCount = failedSourceCount,
            CreatedLeadCount = createdLeadCount,
            UpdatedLeadCount = updatedLeadCount,
            Message = failedSourceCount == 0
                ? "Google Sheets lead import completed."
                : "Google Sheets lead import completed with failures."
        };
    }

    public async Task<GoogleSheetsLeadIntegrationRunDto> ExportLeadOpsSnapshotAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.ExportEnabled)
        {
            return new GoogleSheetsLeadIntegrationRunDto
            {
                Operation = "export",
                Message = "Google Sheets export is disabled."
            };
        }

        if (string.IsNullOrWhiteSpace(_options.ExportWebhookUrl))
        {
            return new GoogleSheetsLeadIntegrationRunDto
            {
                Operation = "export",
                Message = "Google Sheets export webhook is not configured."
            };
        }

        var inbox = await _leadOpsInboxService.BuildSystemSnapshotAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ExportWebhookUrl.Trim())
        {
            Content = JsonContent.Create(new
            {
                generatedAtUtc = DateTime.UtcNow,
                totals = new
                {
                    inbox.TotalItems,
                    inbox.UrgentCount,
                    inbox.AssignedToMeCount,
                    inbox.UnassignedCount,
                    inbox.NewInboundProspectsCount,
                    inbox.UnassignedProspectsCount,
                    inbox.OpenLeadActionsCount,
                    inbox.NoRecentActivityCount,
                    inbox.AwaitingClientResponsesCount,
                    inbox.OverdueFollowUpsCount
                },
                items = inbox.Items
            })
        };

        if (!string.IsNullOrWhiteSpace(_options.ExportWebhookAuthToken))
        {
            request.Headers.TryAddWithoutValidation(
                string.IsNullOrWhiteSpace(_options.ExportWebhookAuthHeaderName) ? "X-Advertified-Token" : _options.ExportWebhookAuthHeaderName.Trim(),
                _options.ExportWebhookAuthToken.Trim());
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await _changeAuditService.WriteAsync(
            null,
            "system",
            "google_sheets_export_run",
            "lead_ops",
            "google_sheets",
            "Google Sheets lead ops export",
            $"Exported {inbox.Items.Count} Lead Ops items to Google Sheets.",
            new { ExportedItemCount = inbox.Items.Count },
            cancellationToken);

        return new GoogleSheetsLeadIntegrationRunDto
        {
            Operation = "export",
            ExportedItemCount = inbox.Items.Count,
            Message = "Lead Ops snapshot exported to Google Sheets."
        };
    }
}
