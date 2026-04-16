using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/lead-industry-policies")]
public sealed class AdminLeadIndustryPoliciesController : BaseAdminController
{
    private readonly IAdminLeadIndustryPolicyService _policyService;

    public AdminLeadIndustryPoliciesController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        IAdminLeadIndustryPolicyService policyService)
        : base(db, currentUserAccessor, changeAuditService)
    {
        _policyService = policyService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] Contracts.Admin.CreateAdminLeadIndustryPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _policyService.CreateAsync(request, cancellationToken);
            await WriteChangeAuditAsync(
                "create",
                "lead_industry_policy",
                request.Key,
                request.Name,
                $"Created lead industry policy {request.Name}.",
                request,
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(
        string key,
        [FromBody] Contracts.Admin.UpdateAdminLeadIndustryPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        if (!string.Equals(key?.Trim(), request.Key?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Route key must match request key." });
        }

        var normalizedKey = request.Key?.Trim() ?? string.Empty;

        try
        {
            await _policyService.UpdateAsync(normalizedKey, request, cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "lead_industry_policy",
                normalizedKey,
                request.Name,
                $"Updated lead industry policy {request.Name}.",
                request,
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _policyService.DeleteAsync(key, cancellationToken);
            await WriteChangeAuditAsync(
                "delete",
                "lead_industry_policy",
                key,
                key,
                $"Deleted lead industry policy {key}.",
                new { Key = key },
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
