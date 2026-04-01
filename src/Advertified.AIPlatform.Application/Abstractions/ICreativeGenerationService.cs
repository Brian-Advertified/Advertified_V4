using Advertified.AIPlatform.Domain.Models;

namespace Advertified.AIPlatform.Application.Abstractions;

public interface ICreativeGenerationService
{
    Task<CreativeGenerationResult> GenerateAsync(CreativeBrief brief, CancellationToken cancellationToken);
}
