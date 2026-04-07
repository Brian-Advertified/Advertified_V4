using Advertified.App.Contracts.Leads;

namespace Advertified.App.Services.Abstractions;

public interface ILeadSourceAutomationStatusService
{
    LeadSourceAutomationStatusDto GetStatus();
}
