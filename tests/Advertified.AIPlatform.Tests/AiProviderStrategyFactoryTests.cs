using Advertified.AIPlatform.Application.Abstractions;
using Advertified.AIPlatform.Domain.Models;
using Advertified.AIPlatform.Infrastructure;
using NSubstitute;

namespace Advertified.AIPlatform.Tests;

[TestFixture]
public class AiProviderStrategyFactoryTests
{
    [Test]
    public void GetRequired_ReturnsMatchingStrategy()
    {
        var openAi = Substitute.For<IAiProviderStrategy>();
        openAi.CanHandle(AdvertisingChannel.Radio, "creative-generate").Returns(true);

        var factory = new AiProviderStrategyFactory(new[] { openAi });

        var selected = factory.GetRequired(AdvertisingChannel.Radio, "creative-generate");

        Assert.That(selected, Is.SameAs(openAi));
    }

    [Test]
    public void GetRequired_ThrowsWhenMissingStrategy()
    {
        var strategy = Substitute.For<IAiProviderStrategy>();
        strategy.CanHandle(Arg.Any<AdvertisingChannel>(), Arg.Any<string>()).Returns(false);

        var factory = new AiProviderStrategyFactory(new[] { strategy });

        Assert.Throws<InvalidOperationException>(() => factory.GetRequired(AdvertisingChannel.Tv, "asset-video"));
    }
}
