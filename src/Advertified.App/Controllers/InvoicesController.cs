using Advertified.App.Contracts.Invoices;
using Advertified.App.Data;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("invoices")]
[Authorize]
public sealed class InvoicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(AppDbContext db, ICurrentUserAccessor currentUserAccessor, IInvoiceService invoiceService)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _invoiceService = invoiceService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceListItemResponse>>> Get(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        var invoices = await BuildInvoiceQuery(currentUser)
            .AsNoTracking()
            .Include(x => x.LineItems)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(invoices.Select(MapListItem).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        var invoice = await BuildInvoiceQuery(currentUser)
            .AsNoTracking()
            .Include(x => x.LineItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        return Ok(MapDetail(invoice));
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        var invoiceExists = await BuildInvoiceQuery(currentUser)
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, cancellationToken);

        if (!invoiceExists)
        {
            return NotFound();
        }

        var bytes = await _invoiceService.GetPdfBytesAsync(id, cancellationToken);
        return File(bytes, "application/pdf", $"invoice-{id:D}.pdf");
    }

    private async Task<Data.Entities.UserAccount> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        return await _db.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User account not found.");
    }

    private IQueryable<Data.Entities.Invoice> BuildInvoiceQuery(Data.Entities.UserAccount currentUser)
    {
        var query = _db.Invoices.AsQueryable();
        if (currentUser.Role != UserRole.Admin)
        {
            query = query.Where(x => x.UserId == currentUser.Id);
        }

        return query;
    }

    private static InvoiceListItemResponse MapListItem(Data.Entities.Invoice invoice)
    {
        return new InvoiceListItemResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.CampaignName,
            invoice.PackageName,
            invoice.Provider,
            invoice.InvoiceType,
            invoice.Status,
            invoice.TotalAmount,
            invoice.Currency,
            invoice.CreatedAtUtc,
            invoice.PaidAtUtc,
            invoice.PaymentReference,
            $"/invoices/{invoice.Id}/pdf");
    }

    private static InvoiceDetailResponse MapDetail(Data.Entities.Invoice invoice)
    {
        var lineItems = invoice.LineItems
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAtUtc)
            .Select(item => new InvoiceLineItemResponse(
                item.Id,
                item.LineType,
                item.Description,
                item.Quantity,
                item.UnitAmount,
                item.SubtotalAmount,
                item.VatAmount,
                item.TotalAmount,
                item.SortOrder))
            .ToArray();

        return new InvoiceDetailResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.CampaignName,
            invoice.PackageName,
            invoice.Provider,
            invoice.InvoiceType,
            invoice.Status,
            invoice.TotalAmount,
            invoice.Currency,
            lineItems.Sum(x => x.SubtotalAmount),
            lineItems.Sum(x => x.VatAmount),
            invoice.CreatedAtUtc,
            invoice.DueAtUtc,
            invoice.PaidAtUtc,
            invoice.PaymentReference,
            invoice.CustomerName,
            invoice.CustomerEmail,
            invoice.CustomerAddress,
            invoice.CompanyName,
            invoice.CompanyRegistrationNumber,
            invoice.CompanyVatNumber,
            invoice.CompanyAddress,
            lineItems,
            $"/invoices/{invoice.Id}/pdf");
    }
}
