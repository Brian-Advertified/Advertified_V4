using Advertified.AIPlatform.Application.Abstractions;
using Advertified.AIPlatform.Domain.Models;
using Advertified.AIPlatform.Infrastructure;
using NSubstitute;

namespace Advertified.AIPlatform.Tests;

[TestFixture]
public class MultiAiOrchestratorTests
{
    [Test]
    public async Task ExecuteAsync_DelegatesToFactoryStrategy()
    {
        var strategy = Substitute.For<IAiProviderStrategy>();
        strategy.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"result\":\"ok\"}");

        var factory = Substitute.For<IAiProviderStrategyFactory>();
        factory.GetRequired(AdvertisingChannel.Radio, "creative-generate")
            .Returns(strategy);

        var orchestrator = new MultiAiOrchestrator(factory);

        var output = await orchestrator.ExecuteAsync(
            AdvertisingChannel.Radio,
            "creative-generate",
            "{\"input\":\"payload\"}",
            CancellationToken.None);

        Assert.That(output, Is.EqualTo("{\"result\":\"ok\"}"));
        await strategy.Received(1).ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
