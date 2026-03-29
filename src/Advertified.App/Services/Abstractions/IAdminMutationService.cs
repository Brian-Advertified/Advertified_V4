using Advertified.App.Contracts.Admin;

namespace Advertified.App.Services.Abstractions;

public interface IAdminMutationService
{
    Task<AdminOutletDetailResponse> GetOutletAsync(string code, CancellationToken cancellationToken);
    Task<AdminOutletPricingResponse> GetOutletPricingAsync(string code, CancellationToken cancellationToken);
    Task<AdminOutletMutationResponse> CreateOutletAsync(CreateAdminOutletRequest request, CancellationToken cancellationToken);
    Task<AdminOutletMutationResponse> UpdateOutletAsync(string existingCode, UpdateAdminOutletRequest request, CancellationToken cancellationToken);
    Task DeleteOutletAsync(string code, CancellationToken cancellationToken);
    Task<Guid> CreateOutletPricingPackageAsync(string code, UpsertAdminOutletPricingPackageRequest request, CancellationToken cancellationToken);
    Task UpdateOutletPricingPackageAsync(string code, Guid packageId, UpsertAdminOutletPricingPackageRequest request, CancellationToken cancellationToken);
    Task DeleteOutletPricingPackageAsync(string code, Guid packageId, CancellationToken cancellationToken);
    Task<Guid> CreateOutletSlotRateAsync(string code, UpsertAdminOutletSlotRateRequest request, CancellationToken cancellationToken);
    Task UpdateOutletSlotRateAsync(string code, Guid slotRateId, UpsertAdminOutletSlotRateRequest request, CancellationToken cancellationToken);
    Task DeleteOutletSlotRateAsync(string code, Guid slotRateId, CancellationToken cancellationToken);
    Task<AdminRateCardUploadResponse> UploadRateCardAsync(string channel, string? supplierOrStation, string? documentTitle, string? notes, IFormFile file, CancellationToken cancellationToken);
    Task UpdatePreviewRuleAsync(string packageCode, string tierCode, UpdateAdminPreviewRuleRequest request, CancellationToken cancellationToken);
}
