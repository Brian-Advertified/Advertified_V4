using Advertified.App.Contracts.Packages;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class PackagePurchaseService : IPackagePurchaseService
{
    private readonly AppDbContext _db;
    private readonly ICampaignAccessService _accessService;
    private readonly IInvoiceService _invoiceService;
    private readonly IVodaPayCheckoutService _vodaPayCheckoutService;
    private readonly IPaymentStateCache _paymentStateCache;
    private readonly IAgentAreaRoutingService _agentAreaRoutingService;
    private readonly IPricingSettingsProvider _pricingSettingsProvider;

    public PackagePurchaseService(
        AppDbContext db,
        ICampaignAccessService accessService,
        IInvoiceService invoiceService,
        IVodaPayCheckoutService vodaPayCheckoutService,
        IPaymentStateCache paymentStateCache,
        IAgentAreaRoutingService agentAreaRoutingService,
        IPricingSettingsProvider pricingSettingsProvider)
    {
        _db = db;
        _accessService = accessService;
        _invoiceService = invoiceService;
        _vodaPayCheckoutService = vodaPayCheckoutService;
        _paymentStateCache = paymentStateCache;
        _agentAreaRoutingService = agentAreaRoutingService;
        _pricingSettingsProvider = pricingSettingsProvider;
    }

    public async Task<CreatePackageOrderResponse> CreatePendingOrderAsync(Guid userId, CreatePackageOrderRequest request, CancellationToken cancellationToken)
    {
        await _accessService.EnsureCanCreateOrderAsync(userId, cancellationToken);
        var paymentProvider = string.IsNullOrWhiteSpace(request.PaymentProvider)
            ? "vodapay"
            : request.PaymentProvider.Trim().ToLowerInvariant();

        var band = await _db.PackageBands
            .FirstOrDefaultAsync(x => x.Id == request.PackageBandId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Package band not found.");
        var user = await _db.UserAccounts
            .Include(x => x.BusinessProfile)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User account not found.");
        var selectedBudget = request.Amount;
        if (selectedBudget < band.MinBudget || selectedBudget > band.MaxBudget)
        {
            throw new InvalidOperationException(
                $"Selected budget must be between {band.MinBudget:0.##} and {band.MaxBudget:0.##}.");
        }

        var pricingSettings = await _pricingSettingsProvider.GetCurrentAsync(cancellationToken);
        var aiStudioReserveAmount = PricingPolicy.CalculateAiStudioReserveAmount(selectedBudget, pricingSettings.AiStudioReservePercent);
        var chargedAmount = PricingPolicy.CalculateChargedAmount(selectedBudget, pricingSettings.AiStudioReservePercent);

        var now = DateTime.UtcNow;
        var order = new PackageOrder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PackageBandId = band.Id,
            Amount = chargedAmount,
            SelectedBudget = selectedBudget,
            AiStudioReservePercent = pricingSettings.AiStudioReservePercent,
            AiStudioReserveAmount = aiStudioReserveAmount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            PaymentProvider = paymentProvider,
            PaymentStatus = "pending",
            RefundStatus = "none",
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.PackageOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        if (string.Equals(paymentProvider, "lula", StringComparison.OrdinalIgnoreCase))
        {
            var invoice = await _invoiceService.EnsureInvoiceAsync(
                order,
                band,
                user,
                user.BusinessProfile,
                invoiceType: "manual_lula",
                status: InvoiceStatuses.Issued,
                dueAtUtc: DateTime.UtcNow.AddDays(7),
                paidAtUtc: null,
                paymentReference: null,
                sendInvoiceEmail: false,
                cancellationToken);

            return new CreatePackageOrderResponse
            {
                PackageOrderId = order.Id,
                PackageBandId = order.PackageBandId,
                PaymentStatus = order.PaymentStatus,
                Amount = chargedAmount,
                Currency = order.Currency,
                PaymentProvider = order.PaymentProvider ?? paymentProvider,
                InvoiceId = invoice.Id,
                InvoiceStatus = invoice.Status,
                InvoicePdfUrl = $"/invoices/{invoice.Id}/pdf"
            };
        }

        var checkout = await _vodaPayCheckoutService.InitiateAsync(
            order,
            band,
            user,
            user.BusinessProfile,
            cancellationToken);

        order.PaymentReference = checkout.ProviderReference ?? checkout.SessionId;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await UpdatePaymentCacheAsync(order, band.Code, order.PaymentReference, cancellationToken);

        return new CreatePackageOrderResponse
        {
            PackageOrderId = order.Id,
            PackageBandId = order.PackageBandId,
            PaymentStatus = order.PaymentStatus,
            Amount = chargedAmount,
            Currency = order.Currency,
            PaymentProvider = order.PaymentProvider ?? paymentProvider,
            CheckoutUrl = checkout.CheckoutUrl,
            CheckoutSessionId = checkout.SessionId,
            InvoiceId = order.Invoice?.Id,
            InvoiceStatus = order.Invoice?.Status,
            InvoicePdfUrl = order.Invoice is null ? null : $"/invoices/{order.Invoice.Id}/pdf"
        };
    }

    public Task MarkOrderPaidAsync(Guid packageOrderId, string paymentReference, CancellationToken cancellationToken)
        => MarkOrderPaidInternalAsync(packageOrderId, paymentReference, cancellationToken);

    public Task MarkOrderFailedAsync(Guid packageOrderId, string? paymentReference, CancellationToken cancellationToken)
        => MarkOrderFailedInternalAsync(packageOrderId, paymentReference, cancellationToken);

    private async Task MarkOrderPaidInternalAsync(Guid packageOrderId, string paymentReference, CancellationToken cancellationToken)
    {
        var order = await _db.PackageOrders
            .Include(x => x.PackageBand)
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.Campaign)
            .FirstOrDefaultAsync(x => x.Id == packageOrderId, cancellationToken)
            ?? throw new InvalidOperationException("Package order not found.");

        if (order.PaymentStatus == "paid")
        {
            return;
        }

        order.PaymentStatus = "paid";
        order.PaymentReference = paymentReference;
        order.PurchasedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        if (order.Campaign is null)
        {
            var campaign = new Campaign
            {
                Id = Guid.NewGuid(),
                UserId = order.UserId,
                PackageOrderId = order.Id,
                PackageBandId = order.PackageBandId,
                Status = "paid",
                AiUnlocked = false,
                AgentAssistanceRequested = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Campaigns.Add(campaign);
            await _db.SaveChangesAsync(cancellationToken);
            order.Campaign = campaign;
        }

        if (order.Campaign is not null)
        {
            await _agentAreaRoutingService.TryAssignCampaignAsync(order.Campaign.Id, "payment_completed", cancellationToken);
        }

        await _invoiceService.EnsureInvoiceAsync(
            order,
            order.PackageBand,
            order.User,
            order.User.BusinessProfile,
            invoiceType: "tax_invoice",
            status: InvoiceStatuses.Paid,
            dueAtUtc: order.PurchasedAt,
            paidAtUtc: order.PurchasedAt,
            paymentReference,
            sendInvoiceEmail: string.Equals(order.PaymentProvider, "vodapay", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        await UpdatePaymentCacheAsync(order, order.PackageBand.Code, order.PaymentReference, cancellationToken);
    }

    private async Task MarkOrderFailedInternalAsync(Guid packageOrderId, string? paymentReference, CancellationToken cancellationToken)
    {
        var order = await _db.PackageOrders
            .Include(x => x.PackageBand)
            .FirstOrDefaultAsync(x => x.Id == packageOrderId, cancellationToken)
            ?? throw new InvalidOperationException("Package order not found.");

        if (order.PaymentStatus == "paid")
        {
            return;
        }

        order.PaymentStatus = "failed";
        if (!string.IsNullOrWhiteSpace(paymentReference))
        {
            order.PaymentReference = paymentReference;
        }

        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await UpdatePaymentCacheAsync(order, order.PackageBand.Code, order.PaymentReference, cancellationToken);
    }

    private Task UpdatePaymentCacheAsync(PackageOrder order, string packageCode, string? paymentReference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(paymentReference))
        {
            return Task.CompletedTask;
        }

        return _paymentStateCache.SetAsync(paymentReference, new Contracts.Payments.PaymentStateCacheEntry
        {
            Status = order.PaymentStatus,
            Amount = order.Amount,
            Package = string.IsNullOrWhiteSpace(packageCode) ? order.PackageBandId.ToString("D") : packageCode,
            Currency = order.Currency,
            Provider = order.PaymentProvider ?? "unknown",
            PackageOrderId = order.Id,
            UpdatedAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }
}
