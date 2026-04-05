using System.Text.RegularExpressions;
using Advertified.App.Contracts.Admin;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/package-orders")]
[Authorize(Roles = "Admin")]
public sealed class AdminPackageOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPackagePurchaseService _packagePurchaseService;
    private readonly IInvoiceService _invoiceService;
    private readonly IPrivateDocumentStorage _privateDocumentStorage;
    private readonly IChangeAuditService _changeAuditService;
    private readonly ITemplatedEmailService _emailService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<AdminPackageOrdersController> _logger;

    public AdminPackageOrdersController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IPackagePurchaseService packagePurchaseService,
        IInvoiceService invoiceService,
        IPrivateDocumentStorage privateDocumentStorage,
        IChangeAuditService changeAuditService,
        ITemplatedEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<AdminPackageOrdersController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _packagePurchaseService = packagePurchaseService;
        _invoiceService = invoiceService;
        _privateDocumentStorage = privateDocumentStorage;
        _changeAuditService = changeAuditService;
        _emailService = emailService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<AdminPackageOrdersResponse>> Get(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var orders = await _db.PackageOrders
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.Invoice)
            .Include(x => x.Campaign)
            .OrderByDescending(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return Ok(new AdminPackageOrdersResponse
        {
            Items = orders.Select(MapResponse).ToArray()
        });
    }

    [HttpPost("{orderId:guid}/payment-status")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<ActionResult<AdminPackageOrderItemResponse>> UpdatePaymentStatus(
        Guid orderId,
        [FromForm] AdminUpdatePackageOrderPaymentStatusRequest request,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var order = await LoadOrderAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        if (!string.Equals(order.PaymentProvider, "lula", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Manual admin payment updates are currently supported for Lula orders only." });
        }

        if (!string.Equals(order.PaymentStatus, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only pending Lula orders can be updated manually." });
        }

        var normalizedStatus = NormalizeRequiredText(request.PaymentStatus)?.ToLowerInvariant();
        if (normalizedStatus is not ("paid" or "failed"))
        {
            return BadRequest(new { message = "Payment status must be either paid or failed." });
        }

        var note = NormalizeOptionalText(request.Notes);
        if (string.IsNullOrWhiteSpace(note))
        {
            return BadRequest(new { message = "An admin note is required when updating a Lula payment." });
        }

        if (file is null)
        {
            return BadRequest(new { message = "Upload a PDF before saving a Lula payment update." });
        }

        var paymentReference = NormalizeOptionalText(request.PaymentReference);
        if (normalizedStatus == "paid")
        {
            await _packagePurchaseService.MarkOrderPaidAsync(
                order.Id,
                paymentReference ?? order.PaymentReference ?? $"lula-manual-{order.Id:D}",
                cancellationToken);
        }
        else
        {
            await _packagePurchaseService.MarkOrderFailedAsync(order.Id, paymentReference, cancellationToken);
        }

        var refreshedOrder = await LoadOrderAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Package order not found after update.");

        if (normalizedStatus == "failed" && refreshedOrder.Invoice is not null)
        {
            await _invoiceService.EnsureInvoiceAsync(
                refreshedOrder,
                refreshedOrder.PackageBand,
                refreshedOrder.User,
                refreshedOrder.User.BusinessProfile,
                invoiceType: refreshedOrder.Invoice.InvoiceType,
                status: InvoiceStatuses.Cancelled,
                dueAtUtc: refreshedOrder.Invoice.DueAtUtc,
                paidAtUtc: null,
                paymentReference: paymentReference ?? refreshedOrder.Invoice.PaymentReference,
                sendInvoiceEmail: false,
                cancellationToken);

            refreshedOrder = await LoadOrderAsync(orderId, cancellationToken)
                ?? throw new InvalidOperationException("Package order not found after invoice refresh.");
        }

        if (!LooksLikePdf(file))
        {
            return BadRequest(new { message = "Only PDF files can be uploaded for Lula payment updates." });
        }

        var uploadedBytes = await ReadAllBytesAsync(file, cancellationToken);
        if (!HasPdfSignature(uploadedBytes))
        {
            return BadRequest(new { message = "The uploaded file is not a valid PDF document." });
        }

        if (refreshedOrder.Invoice is null)
        {
            return BadRequest(new { message = "This order does not have an invoice to attach the PDF to." });
        }

        var sanitizedFileName = SanitizeFileName(file.FileName);
        var objectKey = InvoiceStoragePathBuilder.BuildSupportingDocumentObjectKey(refreshedOrder.Invoice.InvoiceNumber, sanitizedFileName);
        refreshedOrder.Invoice.SupportingDocumentStorageObjectKey = await _privateDocumentStorage.SaveAsync(objectKey, uploadedBytes, "application/pdf", cancellationToken);
        refreshedOrder.Invoice.SupportingDocumentFileName = sanitizedFileName;
        refreshedOrder.Invoice.SupportingDocumentUploadedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            action: normalizedStatus == "paid" ? "mark_package_order_paid" : "mark_package_order_failed",
            order: refreshedOrder,
            note: note,
            uploadedDocument: refreshedOrder.Invoice?.SupportingDocumentFileName,
            cancellationToken);

        await SendLulaStatusEmailAsync(refreshedOrder, note, normalizedStatus, cancellationToken);

        return Ok(MapResponse(refreshedOrder));
    }

    [HttpGet("{orderId:guid}/supporting-document")]
    public async Task<IActionResult> DownloadSupportingDocument(Guid orderId, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var order = await _db.PackageOrders
            .AsNoTracking()
            .Include(x => x.Invoice)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        var objectKey = order?.Invoice?.SupportingDocumentStorageObjectKey;
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return NotFound();
        }

        var bytes = await _privateDocumentStorage.GetBytesAsync(objectKey, cancellationToken);
        var fileName = string.IsNullOrWhiteSpace(order?.Invoice?.SupportingDocumentFileName)
            ? $"lula-supporting-document-{orderId:D}.pdf"
            : order.Invoice.SupportingDocumentFileName;
        return File(bytes, "application/pdf", fileName);
    }

    private async Task<PackageOrder?> LoadOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await _db.PackageOrders
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.PackageBand)
            .Include(x => x.Invoice)
            .Include(x => x.Campaign)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
    }

    private async Task<ActionResult?> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var user = await _db.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { message = "User account not found." });
        }

        return user.Role == UserRole.Admin
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Admin access required." });
    }

    private async Task WriteChangeAuditAsync(string action, PackageOrder order, string? note, string? uploadedDocument, CancellationToken cancellationToken)
    {
        var actorUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        await _changeAuditService.WriteAsync(
            actorUserId,
            scope: "billing",
            action: action,
            entityType: "package_order",
            entityId: order.Id.ToString("D"),
            entityLabel: order.Invoice?.InvoiceNumber ?? order.Id.ToString("D"),
            summary: $"{order.PackageBand.Name} order for {order.User.FullName} marked {order.PaymentStatus}.",
            metadata: new
            {
                PackageOrderId = order.Id,
                order.PaymentStatus,
                order.PaymentReference,
                UploadedDocument = uploadedDocument,
                Note = note
            },
            cancellationToken);
    }

    private async Task SendLulaStatusEmailAsync(PackageOrder order, string note, string normalizedStatus, CancellationToken cancellationToken)
    {
        if (order.Invoice is null)
        {
            return;
        }

        var templateName = normalizedStatus == "paid" ? "payment-approved-lula" : "payment-declined-lula";

        try
        {
            await _emailService.SendAsync(
                templateName,
                order.User.Email,
                "billing",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = order.User.FullName,
                    ["CampaignName"] = order.Campaign?.CampaignName?.Trim() ?? $"{order.PackageBand.Name} campaign",
                    ["InvoiceNumber"] = order.Invoice.InvoiceNumber,
                    ["PackageName"] = order.PackageBand.Name,
                    ["Amount"] = FormatCurrency(order.Amount),
                    ["PaymentReference"] = order.PaymentReference ?? "-",
                    ["AdminNoteBlock"] = BuildAdminNoteBlock(note),
                    ["OrdersUrl"] = BuildFrontendUrl("/orders")
                },
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Lula status email for package order {PackageOrderId}.", order.Id);
        }
    }

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private static string BuildAdminNoteBlock(string? note)
    {
        var normalized = NormalizeOptionalText(note);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return $@"
            <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
              <p style=""margin:0 0 8px;font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Admin note</p>
              <p style=""margin:0;font-size:15px;line-height:1.7;color:#4b635a;"">{System.Net.WebUtility.HtmlEncode(normalized)}</p>
            </div>";
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount:N2}";
    }

    private static AdminPackageOrderItemResponse MapResponse(PackageOrder order)
    {
        return new AdminPackageOrderItemResponse
        {
            OrderId = order.Id,
            UserId = order.UserId,
            ClientName = order.User.FullName,
            ClientEmail = order.User.Email,
            ClientPhone = order.User.Phone,
            PackageBandId = order.PackageBandId,
            PackageBandName = order.PackageBand.Name,
            SelectedBudget = order.SelectedBudget ?? order.Amount,
            ChargedAmount = order.Amount,
            Currency = order.Currency,
            PaymentProvider = order.PaymentProvider ?? string.Empty,
            PaymentStatus = order.PaymentStatus,
            PaymentReference = order.PaymentReference,
            CreatedAt = new DateTimeOffset(order.CreatedAt, TimeSpan.Zero),
            PurchasedAt = order.PurchasedAt.HasValue ? new DateTimeOffset(order.PurchasedAt.Value, TimeSpan.Zero) : null,
            CampaignId = order.Campaign?.Id,
            CampaignName = order.Campaign?.CampaignName,
            InvoiceId = order.Invoice?.Id,
            InvoiceStatus = order.Invoice?.Status,
            InvoicePdfUrl = order.Invoice is null ? null : $"/invoices/{order.Invoice.Id}/pdf",
            SupportingDocumentPdfUrl = order.Invoice?.SupportingDocumentStorageObjectKey is null ? null : $"/admin/package-orders/{order.Id:D}/supporting-document",
            SupportingDocumentFileName = order.Invoice?.SupportingDocumentFileName,
            SupportingDocumentUploadedAt = order.Invoice?.SupportingDocumentUploadedAtUtc.HasValue == true
                ? new DateTimeOffset(order.Invoice.SupportingDocumentUploadedAtUtc.Value, TimeSpan.Zero)
                : null,
            CanUpdateLulaStatus = string.Equals(order.PaymentProvider, "lula", StringComparison.OrdinalIgnoreCase)
                && string.Equals(order.PaymentStatus, "pending", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool LooksLikePdf(IFormFile file)
    {
        return string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(file.ContentType)
                || string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
                || string.Equals(file.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<byte[]> ReadAllBytesAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    private static bool HasPdfSignature(byte[] bytes)
    {
        return bytes.Length >= 5
            && bytes[0] == 0x25
            && bytes[1] == 0x50
            && bytes[2] == 0x44
            && bytes[3] == 0x46
            && bytes[4] == 0x2D;
    }

    private static string SanitizeFileName(string fileName)
    {
        var normalized = Path.GetFileName(fileName);
        var safe = Regex.Replace(normalized, @"[^A-Za-z0-9._-]", "-");
        return string.IsNullOrWhiteSpace(safe) ? "document.pdf" : safe;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeRequiredText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
