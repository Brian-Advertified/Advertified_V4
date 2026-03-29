using Advertified.App.Services.BroadcastMatching;

namespace Advertified.App.Services.Abstractions;

public interface IBroadcastMatchingEngine
{
    BroadcastMatchResponse Match(IEnumerable<BroadcastMediaOutlet> outlets, BroadcastMatchRequest request);
}
