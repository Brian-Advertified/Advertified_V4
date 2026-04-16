using Advertified.App.Contracts.Packages;
using Advertified.App.Campaigns;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

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
    private readonly ITemplatedEmailService _emailService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<PackagePurchaseService> _logger;

    public PackagePurchaseService(
        AppDbContext db,
        ICampaignAccessService accessService,
        IInvoiceService invoiceService,
        IVodaPayCheckoutService vodaPayCheckoutService,
        IPaymentStateCache paymentStateCache,
        IAgentAreaRoutingService agentAreaRoutingService,
        IPricingSettingsProvider pricingSettingsProvider,
        ITemplatedEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<PackagePurchaseService> logger)
    {
        _db = db;
        _accessService = accessService;
        _invoiceService = invoiceService;
        _vodaPayCheckoutService = vodaPayCheckoutService;
        _paymentStateCache = paymentStateCache;
        _agentAreaRoutingService = agentAreaRoutingService;
        _pricingSettingsProvider = pricingSettingsProvider;
        _emailService = emailService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
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
        var aiStudioReserveAmount = PricingPolicy.CalculateAiStudioReserveAmount(selectedBudget, pricingSettings.AiStudioReservePercent, band.Code, band.Name);
        var chargedAmount = PricingPolicy.CalculateChargedAmount(selectedBudget, pricingSettings.AiStudioReservePercent);

        var now = DateTime.UtcNow;
        var order = new PackageOrder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PackageBandId = band.Id,
            Amount = chargedAmount,
            SelectedBudget = selectedBudget,
            AiStudioReservePercent = aiStudioReserveAmount > 0m ? pricingSettings.AiStudioReservePercent : 0m,
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

        var existingCampaign = await _db.Campaigns
            .FirstOrDefaultAsync(x => x.PackageOrderId == order.Id, cancellationToken);

        if (existingCampaign is null)
        {
            _db.Campaigns.Add(new Campaign
            {
                Id = Guid.NewGuid(),
                UserId = order.UserId,
                PackageOrderId = order.Id,
                PackageBandId = order.PackageBandId,
                Status = CampaignStatuses.AwaitingPurchase,
                AiUnlocked = false,
                AgentAssistanceRequested = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        var campaignForOrder = existingCampaign
            ?? await _db.Campaigns.FirstOrDefaultAsync(x => x.PackageOrderId == order.Id, cancellationToken);
        await SendAdminSaleAlertAsync(order, band, user, paymentProvider, campaignForOrder, cancellationToken);

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

            await SendLulaSubmittedEmailAsync(order, band, user, cancellationToken);

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

    public async Task<CreatePackageOrderResponse> InitiateCheckoutAsync(Guid userId, Guid packageOrderId, string paymentProvider, Guid? recommendationId, CancellationToken cancellationToken)
    {
        await _accessService.EnsureCanCreateOrderAsync(userId, cancellationToken);
        var normalizedProvider = string.IsNullOrWhiteSpace(paymentProvider)
            ? "vodapay"
            : paymentProvider.Trim().ToLowerInvariant();

        if (!string.Equals(normalizedProvider, "vodapay", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedProvider, "lula", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unsupported payment provider.");
        }

        var order = await _db.PackageOrders
            .Include(x => x.PackageBand)
            .Include(x => x.User!)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.Campaign)
            .Include(x => x.Invoice)
            .FirstOrDefaultAsync(x => x.Id == packageOrderId && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Package order not found.");
        var purchaser = RequireOrderUser(order);
        var shouldSendLulaSubmittedEmail =
            string.Equals(normalizedProvider, "lula", StringComparison.OrdinalIgnoreCase)
            && (
                !string.Equals(order.PaymentProvider, "lula", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(order.PaymentStatus, "pending", StringComparison.OrdinalIgnoreCase)
            );

        if (string.Equals(order.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This package order is already paid.");
        }

        if (order.Campaign is not null
            && !string.Equals(order.Campaign.Status, CampaignStatuses.AwaitingPurchase, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(order.PaymentStatus, "pending", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(order.PaymentStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This package order cannot be checked out in its current state.");
        }

        if (recommendationId.HasValue)
        {
            await AlignOrderToRecommendationAsync(order, recommendationId.Value, cancellationToken);
        }

        order.PaymentProvider = normalizedProvider;
        order.PaymentStatus = "pending";
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (string.Equals(normalizedProvider, "lula", StringComparison.OrdinalIgnoreCase))
        {
            var invoice = await _invoiceService.EnsureInvoiceAsync(
                order,
                order.PackageBand,
                purchaser,
                purchaser.BusinessProfile,
                invoiceType: "manual_lula",
                status: InvoiceStatuses.Issued,
                dueAtUtc: DateTime.UtcNow.AddDays(7),
                paidAtUtc: null,
                paymentReference: null,
                sendInvoiceEmail: false,
                cancellationToken);

            if (shouldSendLulaSubmittedEmail)
            {
                await SendLulaSubmittedEmailAsync(order, order.PackageBand, purchaser, cancellationToken);
            }

            return new CreatePackageOrderResponse
            {
                PackageOrderId = order.Id,
                PackageBandId = order.PackageBandId,
                PaymentStatus = order.PaymentStatus,
                Amount = order.Amount,
                Currency = order.Currency,
                PaymentProvider = order.PaymentProvider ?? normalizedProvider,
                InvoiceId = invoice.Id,
                InvoiceStatus = invoice.Status,
                InvoicePdfUrl = $"/invoices/{invoice.Id}/pdf"
            };
        }

        var checkout = await _vodaPayCheckoutService.InitiateAsync(
            order,
            order.PackageBand,
            purchaser,
            purchaser.BusinessProfile,
            cancellationToken);

        order.PaymentReference = checkout.ProviderReference ?? checkout.SessionId;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await UpdatePaymentCacheAsync(order, order.PackageBand.Code, order.PaymentReference, cancellationToken);

        return new CreatePackageOrderResponse
        {
            PackageOrderId = order.Id,
            PackageBandId = order.PackageBandId,
            PaymentStatus = order.PaymentStatus,
            Amount = order.Amount,
            Currency = order.Currency,
            PaymentProvider = order.PaymentProvider ?? normalizedProvider,
            CheckoutUrl = checkout.CheckoutUrl,
            CheckoutSessionId = checkout.SessionId,
            InvoiceId = order.Invoice?.Id,
            InvoiceStatus = order.Invoice?.Status,
            InvoicePdfUrl = order.Invoice is null ? null : $"/invoices/{order.Invoice.Id}/pdf"
        };
    }

    public async Task PrepareRecommendationCheckoutAsync(Guid campaignId, Guid recommendationId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (campaign.PackageOrder is null)
        {
            throw new InvalidOperationException("This campaign does not have a package order.");
        }

        if (string.Equals(campaign.PackageOrder.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await AlignOrderToRecommendationAsync(campaign.PackageOrder, recommendationId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task AlignOrderToRecommendationAsync(PackageOrder order, Guid recommendationId, CancellationToken cancellationToken)
    {
        if (order.Campaign is null)
        {
            throw new InvalidOperationException("This package order is not linked to a campaign recommendation.");
        }

        var recommendation = await _db.CampaignRecommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == recommendationId && x.CampaignId == order.Campaign.Id,
                cancellationToken)
            ?? throw new InvalidOperationException("Recommendation not found for this package order.");

        if (recommendation.TotalCost <= 0m)
        {
            throw new InvalidOperationException("Recommendation total is not valid for payment.");
        }

        var alignedAmount = decimal.Round(recommendation.TotalCost, 2, MidpointRounding.AwayFromZero);
        order.Amount = alignedAmount;
        order.SelectedBudget = alignedAmount;
        order.UpdatedAt = DateTime.UtcNow;
    }

    public Task MarkOrderPaidAsync(Guid packageOrderId, string paymentReference, CancellationToken cancellationToken)
        => MarkOrderPaidInternalAsync(packageOrderId, paymentReference, cancellationToken);

    public Task MarkOrderFailedAsync(Guid packageOrderId, string? paymentReference, CancellationToken cancellationToken)
        => MarkOrderFailedInternalAsync(packageOrderId, paymentReference, cancellationToken);

    private async Task MarkOrderPaidInternalAsync(Guid packageOrderId, string paymentReference, CancellationToken cancellationToken)
    {
        var order = await _db.PackageOrders
            .Include(x => x.PackageBand)
            .Include(x => x.User!)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.ProspectLead)
            .Include(x => x.Campaign)
                .ThenInclude(x => x!.CampaignRecommendations)
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
                ProspectLeadId = order.ProspectLeadId,
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
        else if (string.Equals(order.Campaign.Status, CampaignStatuses.AwaitingPurchase, StringComparison.OrdinalIgnoreCase))
        {
            order.Campaign.Status = CampaignStatuses.Paid;
            order.Campaign.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (order.Campaign is not null)
        {
            await _agentAreaRoutingService.TryAssignCampaignAsync(order.Campaign.Id, "payment_completed", cancellationToken);
        }

        if (order.User is not null)
        {
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
        }

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

    private static UserAccount RequireOrderUser(PackageOrder order)
    {
        return order.User
            ?? throw new InvalidOperationException("Package order is missing its purchaser account.");
    }

    private async Task SendAdminSaleAlertAsync(
        PackageOrder order,
        PackageBand band,
        UserAccount user,
        string paymentProvider,
        Campaign? campaign,
        CancellationToken cancellationToken)
    {
        try
        {
            var adminEmails = await _db.UserAccounts
                .AsNoTracking()
                .Where(x => x.Role == UserRole.Admin && x.AccountStatus == AccountStatus.Active)
                .Select(x => x.Email)
                .Distinct()
                .ToArrayAsync(cancellationToken);

            if (adminEmails.Length == 0)
            {
                return;
            }

            var campaignName = campaign is not null && !string.IsNullOrWhiteSpace(campaign.CampaignName)
                ? campaign.CampaignName.Trim()
                : $"{band.Name} campaign";
            var isLula = string.Equals(paymentProvider, "lula", StringComparison.OrdinalIgnoreCase);
            var adminUrl = BuildFrontendUrl("/admin/package-orders");
            var actionNote = isLula
                ? "Finance Partner order created. Review in admin package orders and follow up on settlement workflow."
                : "Track payment progression and campaign activation readiness.";

            foreach (var email in adminEmails)
            {
                await _emailService.SendAsync(
                    "admin-sale-alert",
                    email,
                    "admin-sales",
                    new Dictionary<string, string?>
                    {
                        ["ClientName"] = user.FullName,
                        ["ClientEmail"] = user.Email,
                        ["CampaignName"] = campaignName,
                        ["PackageName"] = band.Name,
                        ["SelectedBudget"] = FormatCurrency(order.SelectedBudget ?? order.Amount),
                        ["ChargedAmount"] = FormatCurrency(order.Amount),
                        ["PaymentProvider"] = order.PaymentProvider ?? paymentProvider,
                        ["PaymentStatus"] = order.PaymentStatus,
                        ["ActionNote"] = actionNote,
                        ["AdminUrl"] = adminUrl
                    },
                    null,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin sale alert for package order {PackageOrderId}.", order.Id);
        }
    }

    private async Task SendLulaSubmittedEmailAsync(
        PackageOrder order,
        PackageBand band,
        UserAccount user,
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                "payment-submitted-lula",
                user.Email,
                "billing",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = user.FullName,
                    ["CampaignName"] = order.Campaign?.CampaignName?.Trim() ?? $"{band.Name} campaign",
                    ["PackageName"] = band.Name,
                    ["Amount"] = FormatCurrency(order.Amount),
                    ["OrdersUrl"] = BuildFrontendUrl("/orders")
                },
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Pay Later submitted email for package order {PackageOrderId}.", order.Id);
        }
    }

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
    }
}
