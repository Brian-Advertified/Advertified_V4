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
    Task<AdminGeographyDetailResponse> GetGeographyAsync(string code, CancellationToken cancellationToken);
    Task<AdminGeographyDetailResponse> CreateGeographyAsync(CreateAdminGeographyRequest request, CancellationToken cancellationToken);
    Task<AdminGeographyDetailResponse> UpdateGeographyAsync(string existingCode, UpdateAdminGeographyRequest request, CancellationToken cancellationToken);
    Task DeleteGeographyAsync(string code, CancellationToken cancellationToken);
    Task<Guid> CreateGeographyMappingAsync(string code, UpsertAdminGeographyMappingRequest request, CancellationToken cancellationToken);
    Task UpdateGeographyMappingAsync(string code, Guid mappingId, UpsertAdminGeographyMappingRequest request, CancellationToken cancellationToken);
    Task DeleteGeographyMappingAsync(string code, Guid mappingId, CancellationToken cancellationToken);
    Task<AdminRateCardUploadResponse> UploadRateCardAsync(string channel, string? supplierOrStation, string? documentTitle, string? notes, IFormFile file, CancellationToken cancellationToken);
    Task UpdateRateCardAsync(string sourceFile, UpdateAdminRateCardRequest request, CancellationToken cancellationToken);
    Task DeleteRateCardAsync(string sourceFile, CancellationToken cancellationToken);
    Task<Guid> CreatePackageSettingAsync(CreateAdminPackageSettingRequest request, CancellationToken cancellationToken);
    Task UpdatePackageSettingAsync(Guid packageSettingId, UpdateAdminPackageSettingRequest request, CancellationToken cancellationToken);
    Task DeletePackageSettingAsync(Guid packageSettingId, CancellationToken cancellationToken);
    Task UpdatePricingSettingsAsync(UpdateAdminPricingSettingsRequest request, CancellationToken cancellationToken);
    Task UpdateEnginePolicyAsync(string packageCode, UpdateAdminEnginePolicyRequest request, CancellationToken cancellationToken);
    Task UpdatePlanningAllocationSettingsAsync(UpdateAdminPlanningAllocationSettingsRequest request, CancellationToken cancellationToken);
    Task UpdatePreviewRuleAsync(string packageCode, string tierCode, UpdateAdminPreviewRuleRequest request, CancellationToken cancellationToken);
}
