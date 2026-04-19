using Advertified.App.Support;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class RecommendationOohPolicyTests
{
    [Fact]
    public void ContainsOoh_TreatsDigitalScreensAsOoh()
    {
        RecommendationOohPolicy.ContainsOoh(new[] { "digital_screen" }).Should().BeTrue();
    }

    [Fact]
    public void ContainsOoh_TreatsBillboardsAsOoh()
    {
        RecommendationOohPolicy.ContainsOoh(new[] { "billboard" }).Should().BeTrue();
    }
}
