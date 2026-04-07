namespace Advertified.App.Services.Abstractions;

public interface ILeadSourceDropFolderProcessor
{
    Task<LeadSourceDropFolderProcessResult> ProcessAsync(CancellationToken cancellationToken);
}
