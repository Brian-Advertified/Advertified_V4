using Advertified.App.Contracts.Packages;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("package-orders")]
public sealed class PackageOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPackagePurchaseService _packagePurchaseService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public PackageOrdersController(AppDbContext db, IPackagePurchaseService packagePurchaseService, ICurrentUserAccessor currentUserAccessor)
    {
        _db = db;
        _packagePurchaseService = packagePurchaseService;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PackageOrderListItemResponse>>> Get(CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var orders = await _db.PackageOrders
            .AsNoTracking()
            .Include(x => x.PackageBand)
            .Include(x => x.Invoice)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return Ok(orders.Select(x => x.ToListItem()).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PackageOrderDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var order = await _db.PackageOrders
            .AsNoTracking()
            .Include(x => x.PackageBand)
            .Include(x => x.Invoice)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        var response = order.ToListItem();
        return Ok(new PackageOrderDetailResponse
        {
            Id = response.Id,
            UserId = response.UserId,
            PackageBandId = response.PackageBandId,
            PackageBandName = response.PackageBandName,
            Amount = response.Amount,
            Currency = response.Currency,
            PaymentProvider = response.PaymentProvider,
            PaymentStatus = response.PaymentStatus,
            RefundStatus = response.RefundStatus,
            RefundedAmount = response.RefundedAmount,
            GatewayFeeRetainedAmount = response.GatewayFeeRetainedAmount,
            RefundReason = response.RefundReason,
            RefundProcessedAt = response.RefundProcessedAt,
            PaymentReference = response.PaymentReference,
            CreatedAt = response.CreatedAt,
            InvoiceId = response.InvoiceId,
            InvoiceStatus = response.InvoiceStatus,
            InvoicePdfUrl = response.InvoicePdfUrl
        });
    }

    [HttpPost]
    public async Task<ActionResult<CreatePackageOrderResponse>> Create(
        [FromBody] CreatePackageOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        try
        {
            var result = await _packagePurchaseService.CreatePendingOrderAsync(
                userId,
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                title: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{id:guid}/checkout")]
    public async Task<ActionResult<CreatePackageOrderResponse>> Checkout(
        Guid id,
        [FromBody] CheckoutPackageOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);

        try
        {
            var result = await _packagePurchaseService.InitiateCheckoutAsync(
                userId,
                id,
                request.PaymentProvider,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                title: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
