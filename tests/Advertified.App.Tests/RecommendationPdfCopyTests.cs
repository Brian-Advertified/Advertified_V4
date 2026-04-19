using Advertified.App.Campaigns;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class RecommendationPdfCopyTests
{
    [Theory]
    [InlineData("digital_screen")]
    [InlineData("billboard")]
    [InlineData("OOH")]
    [InlineData("Billboards and Digital Screens")]
    public void NormalizeRecommendationChannel_TreatsOohFamilyChannelsAsOoh(string channel)
    {
        var normalized = RecommendationPdfCopy.NormalizeRecommendationChannel(channel);

        normalized.Should().Be("ooh");
    }
}
