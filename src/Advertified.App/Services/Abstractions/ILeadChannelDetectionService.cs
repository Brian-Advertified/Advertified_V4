using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ILeadChannelDetectionService
{
    IReadOnlyList<LeadChannelDetectionResult> Detect(
        Lead lead,
        Signal? signal,
        IReadOnlyList<LeadSignalEvidence>? evidences = null,
        MasterIndustryMatch? canonicalIndustry = null);
}
