using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ILeadEnrichmentSnapshotService
{
    LeadEnrichmentSnapshot Build(
        Lead lead,
        Signal? latestSignal,
        IReadOnlyList<LeadSignalEvidence> evidences,
        IReadOnlyList<LeadChannelDetectionResult> channelDetections,
        MasterIndustryMatch? canonicalIndustry = null,
        LeadIndustryContext? industryContext = null);
}
