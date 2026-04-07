using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ITrendAnalysisService
{
    LeadTrendAnalysisResult Analyze(Signal? previousSignal, Signal currentSignal);
}
