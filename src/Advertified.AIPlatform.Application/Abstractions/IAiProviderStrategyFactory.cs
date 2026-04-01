using Advertified.AIPlatform.Domain.Models;

namespace Advertified.AIPlatform.Application.Abstractions;

public interface IAiProviderStrategyFactory
{
    IAiProviderStrategy GetRequired(AdvertisingChannel channel, string operation);
}
