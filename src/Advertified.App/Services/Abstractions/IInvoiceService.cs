using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface IInvoiceService
{
    Task<Invoice> EnsureInvoiceAsync(
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
        CancellationToken cancellationToken);

    Task<byte[]> GetPdfBytesAsync(Guid invoiceId, CancellationToken cancellationToken);
}
