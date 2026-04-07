using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class LeadSourceDropFolderProcessor : ILeadSourceDropFolderProcessor
{
    private readonly LeadSourceDropFolderOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILeadSourceImportService _leadSourceImportService;
    private readonly ILeadIntelligenceOrchestrator _leadIntelligenceOrchestrator;
    private readonly ILogger<LeadSourceDropFolderProcessor> _logger;

    public LeadSourceDropFolderProcessor(
        IWebHostEnvironment environment,
        ILeadSourceImportService leadSourceImportService,
        ILeadIntelligenceOrchestrator leadIntelligenceOrchestrator,
        IOptions<LeadSourceDropFolderOptions> options,
        ILogger<LeadSourceDropFolderProcessor> logger)
    {
        _environment = environment;
        _leadSourceImportService = leadSourceImportService;
        _leadIntelligenceOrchestrator = leadIntelligenceOrchestrator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LeadSourceDropFolderProcessResult> ProcessAsync(CancellationToken cancellationToken)
    {
        var inboxPath = ResolvePath(_options.InboxPath);
        var processedPath = ResolvePath(_options.ProcessedPath);
        var failedPath = ResolvePath(_options.FailedPath);

        Directory.CreateDirectory(inboxPath);
        Directory.CreateDirectory(processedPath);
        Directory.CreateDirectory(failedPath);

        var files = Directory
            .EnumerateFiles(inboxPath, "*.csv", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var processedFileCount = 0;
        var failedFileCount = 0;
        var importedLeadCount = 0;
        var analyzedLeadCount = 0;

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var csvText = await File.ReadAllTextAsync(filePath, cancellationToken);
                var importProfile = InferImportProfile(filePath);
                var defaultSource = ResolveDefaultSource(importProfile);
                var result = await _leadSourceImportService.ImportCsvAsync(
                    csvText,
                    defaultSource,
                    importProfile,
                    cancellationToken);

                importedLeadCount += result.Leads.Count;

                if (_options.AnalyzeImportedLeads)
                {
                    foreach (var lead in result.Leads)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await _leadIntelligenceOrchestrator.RunLeadAsync(lead.Id, cancellationToken);
                        analyzedLeadCount++;
                    }
                }

                MoveFile(filePath, processedPath);
                processedFileCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lead source drop-folder import failed for file {FilePath}.", filePath);
                MoveFile(filePath, failedPath);
                failedFileCount++;
            }
        }

        return new LeadSourceDropFolderProcessResult
        {
            ProcessedFileCount = processedFileCount,
            FailedFileCount = failedFileCount,
            ImportedLeadCount = importedLeadCount,
            AnalyzedLeadCount = analyzedLeadCount,
        };
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, path));
    }

    private string InferImportProfile(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (fileName.Contains("google", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("gmaps", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("maps", StringComparison.OrdinalIgnoreCase))
        {
            return "google_maps";
        }

        return string.IsNullOrWhiteSpace(_options.DefaultImportProfile)
            ? "standard"
            : _options.DefaultImportProfile.Trim();
    }

    private string ResolveDefaultSource(string importProfile)
    {
        if (string.Equals(importProfile, "google_maps", StringComparison.OrdinalIgnoreCase))
        {
            return "google_maps";
        }

        return string.IsNullOrWhiteSpace(_options.DefaultSource)
            ? "csv_drop"
            : _options.DefaultSource.Trim();
    }

    private static void MoveFile(string sourcePath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));
        if (File.Exists(destinationPath))
        {
            var baseName = Path.GetFileNameWithoutExtension(sourcePath);
            var extension = Path.GetExtension(sourcePath);
            destinationPath = Path.Combine(
                destinationDirectory,
                $"{baseName}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}");
        }

        File.Move(sourcePath, destinationPath);
    }
}
