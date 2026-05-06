using System.Globalization;
using Advertified.App.Billing;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class InvoiceService : IInvoiceService
{
    private const decimal VatRate = 0.15m;
    private readonly AppDbContext _db;
    private readonly ITemplatedEmailService _emailService;
    private readonly IWebHostEnvironment _environment;
    private readonly IPrivateDocumentStorage _documentStorage;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(
        AppDbContext db,
        ITemplatedEmailService emailService,
        IWebHostEnvironment environment,
        IPrivateDocumentStorage documentStorage,
        ILogger<InvoiceService> logger)
    {
        _db = db;
        _emailService = emailService;
        _environment = environment;
        _documentStorage = documentStorage;
        _logger = logger;
    }

    public async Task<Invoice> EnsureInvoiceAsync(
        PackageOrder order,
        PackageBand band,
        UserAccount user,
        BusinessProfile? businessProfile,
        string invoiceType,
        string status,
        DateTime? dueAtUtc,
        DateTime? paidAtUtc,
        string? paymentReference,
        bool sendInvoiceEmail,
        CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices
            .Include(x => x.LineItems)
            .FirstOrDefaultAsync(x => x.PackageOrderId == order.Id, cancellationToken);

        if (invoice is null)
        {
            invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                PackageOrderId = order.Id,
                CampaignId = order.Campaign?.Id,
                UserId = user.Id,
                CompanyId = businessProfile?.Id,
                InvoiceNumber = await GenerateInvoiceNumberAsync(cancellationToken),
                Provider = string.IsNullOrWhiteSpace(order.PaymentProvider) ? "vodapay" : order.PaymentProvider!,
                InvoiceType = invoiceType,
                Status = status,
                Currency = order.Currency,
                TotalAmount = order.Amount,
                CampaignName = BuildCampaignName(order, band),
                PackageName = band.Name,
                CustomerName = user.FullName,
                CustomerEmail = user.Email,
                CustomerAddress = BuildCustomerAddress(businessProfile),
                CompanyName = businessProfile?.BusinessName ?? user.FullName,
                CompanyRegistrationNumber = businessProfile?.RegistrationNumber,
                CompanyVatNumber = businessProfile?.VatNumber,
                CompanyAddress = BuildCompanyAddress(businessProfile),
                PaymentReference = paymentReference,
                CreatedAtUtc = DateTime.UtcNow,
                DueAtUtc = dueAtUtc,
                PaidAtUtc = paidAtUtc
            };

            invoice.LineItems.Add(CreateDefaultLineItem(invoice, band, order));
            _db.Invoices.Add(invoice);
        }
        else
        {
            invoice.CampaignId ??= order.Campaign?.Id;
            invoice.Provider = string.IsNullOrWhiteSpace(order.PaymentProvider) ? invoice.Provider : order.PaymentProvider!;
            invoice.InvoiceType = invoiceType;
            invoice.Status = status;
            invoice.Currency = order.Currency;
            invoice.TotalAmount = order.Amount;
            invoice.CampaignName = BuildCampaignName(order, band);
            invoice.PackageName = band.Name;
            invoice.CustomerName = user.FullName;
            invoice.CustomerEmail = user.Email;
            invoice.CustomerAddress = BuildCustomerAddress(businessProfile);
            invoice.CompanyId = businessProfile?.Id;
            invoice.CompanyName = businessProfile?.BusinessName ?? user.FullName;
            invoice.CompanyRegistrationNumber = businessProfile?.RegistrationNumber;
            invoice.CompanyVatNumber = businessProfile?.VatNumber;
            invoice.CompanyAddress = BuildCompanyAddress(businessProfile);
            invoice.PaymentReference = paymentReference ?? invoice.PaymentReference;
            invoice.DueAtUtc = dueAtUtc;
            invoice.PaidAtUtc = paidAtUtc;

            if (invoice.LineItems.Count == 0)
            {
                invoice.LineItems.Add(CreateDefaultLineItem(invoice, band, order));
            }
            else
            {
                var lineItem = invoice.LineItems.OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAtUtc).First();
                UpdateDefaultLineItem(lineItem, invoice, band, order);
            }
        }

        var issuer = await _db.InvoiceIssuerProfiles
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("No active invoice issuer profile is configured.");

        var logoPath = InvoicePdfGenerator.ResolveLogoPath(_environment.ContentRootPath, issuer.LogoPath);
        var pdfBytes = InvoicePdfGenerator.Generate(issuer, invoice, logoPath);
        invoice.StorageObjectKey = await PersistPdfAsync(invoice, pdfBytes, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        if (sendInvoiceEmail)
        {
            await SendInvoiceEmailAsync(invoice, pdfBytes, cancellationToken);
        }

        return invoice;
    }

    public async Task<byte[]> GetPdfBytesAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new InvalidOperationException("Invoice not found.");

        if (string.IsNullOrWhiteSpace(invoice.StorageObjectKey))
        {
            throw new InvalidOperationException("Invoice PDF has not been generated.");
        }

        return await _documentStorage.GetBytesAsync(invoice.StorageObjectKey, cancellationToken);
    }

    private async Task SendInvoiceEmailAsync(Invoice invoice, byte[] pdfBytes, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                "invoice-delivery",
                invoice.CustomerEmail,
                "billing",
                new Dictionary<string, string?>
                {
                    ["CampaignName"] = invoice.CampaignName,
                    ["InvoiceNumber"] = invoice.InvoiceNumber,
                    ["PackageName"] = invoice.PackageName,
                    ["Amount"] = CurrencyFormatSupport.FormatZar(invoice.TotalAmount),
                    ["PaymentReference"] = invoice.PaymentReference ?? "-"
                },
                new[]
                {
                    new EmailAttachment
                    {
                        FileName = $"{invoice.InvoiceNumber}.pdf",
                        ContentType = "application/pdf",
                        Content = pdfBytes
                    }
                },
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invoice delivery email for invoice {InvoiceId}.", invoice.Id);
        }
    }

    private async Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var prefix = $"INV-{DateTime.UtcNow:yyyyMMdd}";
        var existingCount = await _db.Invoices.CountAsync(x => x.InvoiceNumber.StartsWith(prefix), cancellationToken);
        return $"{prefix}-{(existingCount + 1).ToString("D4", CultureInfo.InvariantCulture)}";
    }

    private async Task<string> PersistPdfAsync(Invoice invoice, byte[] pdfBytes, CancellationToken cancellationToken)
    {
        var objectKey = InvoiceStoragePathBuilder.BuildInvoicePdfObjectKey(invoice.InvoiceNumber);
        return await _documentStorage.SaveAsync(objectKey, pdfBytes, "application/pdf", cancellationToken);
    }

    private static InvoiceLineItem CreateDefaultLineItem(Invoice invoice, PackageBand band, PackageOrder order)
    {
        var total = order.Amount;
        var subtotal = Math.Round(total / (1m + VatRate), 2, MidpointRounding.AwayFromZero);
        var vatAmount = Math.Round(total - subtotal, 2, MidpointRounding.AwayFromZero);

        return new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            LineType = "campaign_package",
            Description = $"{band.Name} package ({BuildCampaignName(order, band)})",
            Quantity = 1m,
            UnitAmount = total,
            SubtotalAmount = subtotal,
            VatAmount = vatAmount,
            TotalAmount = total,
            SortOrder = 0,
            CreatedAtUtc = invoice.CreatedAtUtc
        };
    }

    private static void UpdateDefaultLineItem(InvoiceLineItem lineItem, Invoice invoice, PackageBand band, PackageOrder order)
    {
        var total = order.Amount;
        var subtotal = Math.Round(total / (1m + VatRate), 2, MidpointRounding.AwayFromZero);
        var vatAmount = Math.Round(total - subtotal, 2, MidpointRounding.AwayFromZero);

        lineItem.Description = $"{band.Name} package ({BuildCampaignName(order, band)})";
        lineItem.Quantity = 1m;
        lineItem.UnitAmount = total;
        lineItem.SubtotalAmount = subtotal;
        lineItem.VatAmount = vatAmount;
        lineItem.TotalAmount = total;
    }

    private static string BuildCampaignName(PackageOrder order, PackageBand band)
    {
        var campaignName = order.Campaign?.CampaignName?.Trim();
        return !string.IsNullOrWhiteSpace(campaignName) ? campaignName : $"{band.Name} campaign";
    }

    private static string BuildCustomerAddress(BusinessProfile? businessProfile)
    {
        if (businessProfile is null)
        {
            return "Address to be confirmed";
        }

        return string.Join(
            Environment.NewLine,
            new[] { businessProfile.StreetAddress, businessProfile.City, businessProfile.Province }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? BuildCompanyAddress(BusinessProfile? businessProfile)
    {
        if (businessProfile is null)
        {
            return null;
        }

        return BuildCustomerAddress(businessProfile);
    }

}
