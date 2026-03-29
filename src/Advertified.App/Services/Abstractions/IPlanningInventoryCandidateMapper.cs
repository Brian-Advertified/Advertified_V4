using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IPlanningInventoryCandidateMapper
{
    InventoryCandidate MapOoh(OohPlanningInventoryRow row);
    InventoryCandidate MapBroadcast(BroadcastPlanningInventorySeed seed);
}
