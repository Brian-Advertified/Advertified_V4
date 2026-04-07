using Advertified.App.Configuration;
using Advertified.App.Contracts.Leads;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class LeadSourceAutomationStatusService : ILeadSourceAutomationStatusService
{
    private readonly LeadSourceDropFolderOptions _options;
    private readonly IWebHostEnvironment _environment;

    public LeadSourceAutomationStatusService(
        IWebHostEnvironment environment,
        IOptions<LeadSourceDropFolderOptions> options)
    {
        _environment = environment;
        _options = options.Value;
    }

    public LeadSourceAutomationStatusDto GetStatus()
    {
        var inboxPath = ResolvePath(_options.InboxPath);
        var processedPath = ResolvePath(_options.ProcessedPath);
        var failedPath = ResolvePath(_options.FailedPath);

        return new LeadSourceAutomationStatusDto
        {
            DropFolderEnabled = _options.Enabled,
            InboxPath = inboxPath,
            ProcessedPath = processedPath,
            FailedPath = failedPath,
            PendingFileCount = CountCsvFiles(inboxPath),
            ProcessedFileCount = CountCsvFiles(processedPath),
            FailedFileCount = CountCsvFiles(failedPath),
            DefaultSource = string.IsNullOrWhiteSpace(_options.DefaultSource) ? "csv_drop" : _options.DefaultSource.Trim(),
            DefaultImportProfile = string.IsNullOrWhiteSpace(_options.DefaultImportProfile) ? "standard" : _options.DefaultImportProfile.Trim(),
            AnalyzeImportedLeads = _options.AnalyzeImportedLeads
        };
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, path));
    }

    private static int CountCsvFiles(string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        return Directory.EnumerateFiles(path, "*.csv", SearchOption.TopDirectoryOnly).Count();
    }
}
