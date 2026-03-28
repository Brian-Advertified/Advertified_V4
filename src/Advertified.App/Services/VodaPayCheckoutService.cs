using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Payments;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace Advertified.App.Services;

public sealed class VodaPayCheckoutService : IVodaPayCheckoutService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly VodaPayOptions _options;
    private readonly FrontendOptions _frontendOptions;
    private readonly IPaymentAuditService _paymentAuditService;

    public VodaPayCheckoutService(
        HttpClient httpClient,
        IOptions<VodaPayOptions> options,
        IOptions<FrontendOptions> frontendOptions,
        IPaymentAuditService paymentAuditService)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _frontendOptions = frontendOptions.Value;
        _paymentAuditService = paymentAuditService;
    }

    public async Task<VodaPayCheckoutSession> InitiateAsync(
        PackageOrder order,
        PackageBand band,
        UserAccount user,
        BusinessProfile? businessProfile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("VodaPay BaseUrl is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("VodaPay ApiKey is not configured.");
        }

        var sessionId = $"checkout-{order.Id:D}";
        var traceId = $"checkout{order.Id:N}"[..Math.Min(20, $"checkout{order.Id:N}".Length)];
        var payload = BuildPayload(order, band, user, businessProfile, sessionId, traceId);
        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);
        var requestUrl = new Uri(_httpClient.BaseAddress!, _options.InitiatePath.TrimStart('/')).ToString();
        var requestHeadersJson = SerializeHeaders(new Dictionary<string, string[]>
        {
            ["Accept"] = new[] { "application/json" },
            ["Content-Type"] = new[] { "application/json; charset=utf-8" },
            ["api-key"] = new[] { _options.ApiKey },
            ["test"] = new[] { _options.IsTest ? "true" : "false" }
        });
        var requestAuditId = await _paymentAuditService.CreateProviderRequestAsync(
            order.Id,
            "vodapay",
            "checkout_initiate",
            requestUrl,
            requestHeadersJson,
            payloadJson,
            sessionId,
            cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.InitiatePath.TrimStart('/'))
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("api-key", _options.ApiKey);
        request.Headers.Add("test", _options.IsTest ? "true" : "false");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        await _paymentAuditService.CompleteProviderRequestAsync(
            requestAuditId,
            (int)response.StatusCode,
            SerializeHeaders(response.Headers, response.Content.Headers),
            responseBody,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VodaPay initiate request failed with {(int)response.StatusCode}: {responseBody}");
        }

        var providerResponse = JsonSerializer.Deserialize<VodaPayInitiateResponse>(responseBody, SerializerOptions)
            ?? throw new InvalidOperationException($"VodaPay response could not be parsed. Response: {responseBody}");
        var providerData = providerResponse.Data
            ?? throw new InvalidOperationException($"VodaPay response did not include a data payload. Response: {responseBody}");
        var checkoutUrl = providerData.PaymentUrl
            ?? providerData.InitiationUrl
            ?? providerData.CheckoutUrl;

        if (string.IsNullOrWhiteSpace(checkoutUrl))
        {
            throw new InvalidOperationException($"VodaPay response did not contain a payment URL. Response: {responseBody}");
        }

        return new VodaPayCheckoutSession
        {
            SessionId = sessionId,
            CheckoutUrl = checkoutUrl,
            TraceId = traceId,
            EchoData = sessionId,
            ProviderReference = providerData.PaymentReference
                ?? providerData.TransactionId
        };
    }

    private static string SerializeHeaders(params HttpHeaders[] headerSets)
    {
        var dictionary = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var headerSet in headerSets)
        {
            foreach (var header in headerSet)
            {
                dictionary[header.Key] = header.Value.ToArray();
            }
        }

        return SerializeHeaders(dictionary);
    }

    private static string SerializeHeaders(IReadOnlyDictionary<string, string[]> headers)
    {
        return JsonSerializer.Serialize(headers, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    private object BuildPayload(
        PackageOrder order,
        PackageBand band,
        UserAccount user,
        BusinessProfile? businessProfile,
        string sessionId,
        string traceId)
    {
        var amountMinorUnits = ToMinorUnits(order.Amount);
        var vatMinorUnits = CalculateVatMinorUnits(order.Amount, _options.VatRatePercent);
        var amountExVatMinorUnits = amountMinorUnits - vatMinorUnits;
        var callbackUrl = BuildFrontendCallbackUrl(order.Id, sessionId);
        var communicationMessage = string.IsNullOrWhiteSpace(_options.Communication.Message)
            ? null
            : _options.Communication.Message.Replace("{PaymentUrl}", callbackUrl, StringComparison.OrdinalIgnoreCase);
        var description = $"Advertified {band.Name} package";

        return new
        {
            echoData = sessionId,
            traceId,
            amount = amountMinorUnits,
            currency = "710",
            customerId = user.Id.ToString("N"),
            delaySettlement = false,
            basket = new[]
            {
                new
                {
                    lineNumber = "1",
                    Id = $"{band.Code}-{order.Id:N}",
                    barcode = traceId,
                    quantity = 1,
                    description,
                    amountExVAT = amountExVatMinorUnits,
                    amountVAT = vatMinorUnits
                }
            },
            notifications = new
            {
                callbackUrl,
                notificationUrl = _options.NotificationUrl
            },
            additionalData = $"{_options.AdditionalDataPrefix}{order.Id:N}",
            styling = new
            {
                logoUrl = _options.Styling.LogoUrl,
                bannerUrl = _options.Styling.BannerUrl,
                theme = _options.Styling.Theme
            },
            electronicReceipt = new
            {
                method = _options.ElectronicReceipt.Method,
                address = user.Email
            },
            communication = new
            {
                msisdn = user.Phone,
                emailAddress = user.Email,
                message = communicationMessage
            }
        };
    }

    private string BuildFrontendCallbackUrl(Guid orderId, string sessionId)
    {
        var baseUrl = _frontendOptions.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/checkout/confirmation?provider=vodapay&orderId={Uri.EscapeDataString(orderId.ToString())}&session={Uri.EscapeDataString(sessionId)}";
    }

    private static int ToMinorUnits(decimal amount)
    {
        return decimal.ToInt32(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    private static int CalculateVatMinorUnits(decimal amountIncludingVat, decimal vatPercent)
    {
        if (vatPercent <= 0)
        {
            return 0;
        }

        var vatAmount = amountIncludingVat - (amountIncludingVat / (1m + (vatPercent / 100m)));
        return ToMinorUnits(vatAmount);
    }
}
