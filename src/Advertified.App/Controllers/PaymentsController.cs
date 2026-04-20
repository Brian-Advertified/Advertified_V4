using Advertified.App.Contracts.Payments;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Advertified.App.Controllers;

[ApiController]
[Route("payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPackagePurchaseService _packagePurchaseService;
    private readonly IPaymentAuditService _paymentAuditService;
    private readonly IWebhookQueueService _webhookQueueService;

    public PaymentsController(
        IPackagePurchaseService packagePurchaseService,
        IPaymentAuditService paymentAuditService,
        IWebhookQueueService webhookQueueService)
    {
        _packagePurchaseService = packagePurchaseService;
        _paymentAuditService = paymentAuditService;
        _webhookQueueService = webhookQueueService;
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook([FromBody] PaymentWebhookRequest request, CancellationToken cancellationToken)
    {
        var webhookAuditId = await _paymentAuditService.CreateWebhookAsync(
            request.PackageOrderId,
            "generic",
            HttpContext.Request.Path,
            SerializeHeaders(Request.Headers),
            JsonSerializer.Serialize(request),
            "received",
            null,
            cancellationToken);

        if (string.Equals(request.PaymentStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            await _packagePurchaseService.MarkOrderFailedAsync(request.PackageOrderId, request.PaymentReference, cancellationToken);
            await _paymentAuditService.CompleteWebhookAsync(webhookAuditId, "failed", "Payment failure processed.", cancellationToken);
            return Accepted(new { Message = "Payment failure processed." });
        }

        await _packagePurchaseService.MarkOrderPaidAsync(request.PackageOrderId, request.PaymentReference, cancellationToken);
        await _paymentAuditService.CompleteWebhookAsync(webhookAuditId, "paid", "Payment processed.", cancellationToken);
        return Accepted(new { Message = "Payment processed." });
    }

    [HttpPost("webhook/vodapay")]
    [AllowAnonymous]
    public async Task<IActionResult> VodaPayWebhook([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var request = ParseVodaPayWebhookRequest(payload);
        var packageOrderId = ResolvePackageOrderId(request);
        var webhookAuditId = await _paymentAuditService.CreateWebhookAsync(
            packageOrderId,
            "vodapay",
            HttpContext.Request.Path,
            SerializeHeaders(Request.Headers),
            payload.GetRawText(),
            "received",
            null,
            cancellationToken);

        if (packageOrderId is null)
        {
            await _paymentAuditService.CompleteWebhookAsync(webhookAuditId, "rejected", "Could not resolve package order id.", cancellationToken);
            return BadRequest(new { Message = "Could not resolve package order id." });
        }

        var queued = await _webhookQueueService.EnqueueVodaPayWebhookAsync(new QueuedVodaPayWebhookJob
        {
            WebhookAuditId = webhookAuditId,
            Request = request
        }, cancellationToken);

        if (queued)
        {
            await _paymentAuditService.CompleteWebhookAsync(webhookAuditId, "queued", "VodaPay webhook queued for background processing.", cancellationToken);
            return Accepted(new { Message = "VodaPay webhook queued." });
        }

        await ProcessVodaPayWebhookInternalAsync(webhookAuditId, request, cancellationToken);
        return Accepted(new { Message = "VodaPay webhook accepted." });
    }

    [HttpPost("jobs/vodapay-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessQueuedVodaPayWebhook([FromBody] QueuedVodaPayWebhookJob job, CancellationToken cancellationToken)
    {
        await ProcessVodaPayWebhookInternalAsync(job.WebhookAuditId, job.Request, cancellationToken);
        return Ok(new { Message = "Queued VodaPay webhook processed." });
    }

    [HttpPost("callback/vodapay")]
    public async Task<IActionResult> CaptureVodaPayCallback([FromBody] VodaPayCallbackCaptureRequest request, CancellationToken cancellationToken)
    {
        var callbackAuditId = await _paymentAuditService.CreateWebhookAsync(
            request.PackageOrderId,
            "vodapay",
            HttpContext.Request.Path,
            SerializeHeaders(Request.Headers),
            JsonSerializer.Serialize(request),
            "received",
            null,
            cancellationToken);

        var callbackPayload = ResolveCallbackPayload(request.QueryParameters);
        var responseCode = callbackPayload.ResponseCode;
        var paymentReference = callbackPayload.PaymentReference;
        var finalStatus = ResolveVodaPayCallbackStatus(callbackPayload, request.QueryParameters);
        if (finalStatus == "failed")
        {
            await _packagePurchaseService.MarkOrderFailedAsync(request.PackageOrderId, paymentReference, cancellationToken);
        }

        var auditStatus = finalStatus == "paid" ? "pending_validation" : finalStatus;
        var auditMessage = finalStatus switch
        {
            "paid" => $"VodaPay callback returned success code {responseCode}; awaiting webhook validation.",
            "failed" => $"VodaPay callback indicates failed/cancelled payment. Provider message: {callbackPayload.ResponseMessage ?? "No provider message supplied."}",
            _ when string.IsNullOrWhiteSpace(responseCode) => "VodaPay callback saved without response code; awaiting provider webhook.",
            _ => $"VodaPay callback saved with response code {responseCode}: {callbackPayload.ResponseMessage ?? "No provider message supplied."}"
        };

        await _paymentAuditService.CompleteWebhookAsync(callbackAuditId, auditStatus, auditMessage, cancellationToken);
        return Accepted(new { Message = "VodaPay callback saved." });
    }

    private async Task ProcessVodaPayWebhookInternalAsync(Guid webhookAuditId, VodaPayWebhookRequest request, CancellationToken cancellationToken)
    {
        var packageOrderId = ResolvePackageOrderId(request);
        if (packageOrderId is null)
        {
            await _paymentAuditService.CompleteWebhookAsync(webhookAuditId, "rejected", "Could not resolve package order id.", cancellationToken);
            throw new InvalidOperationException("Could not resolve package order id.");
        }

        var paymentReference = ResolvePaymentReference(request) ?? $"vodapay-{packageOrderId:D}";
        var status = ResolvePaymentStatus(request);

        if (status == "paid")
        {
            await _packagePurchaseService.MarkOrderPaidAsync(packageOrderId.Value, paymentReference, cancellationToken);
        }
        else if (status == "failed")
        {
            await _packagePurchaseService.MarkOrderFailedAsync(packageOrderId.Value, paymentReference, cancellationToken);
        }

        await _paymentAuditService.CompleteWebhookAsync(webhookAuditId, status, "VodaPay webhook accepted.", cancellationToken);
    }

    private static string SerializeHeaders(IHeaderDictionary headers)
    {
        var dictionary = headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);

        return JsonSerializer.Serialize(dictionary);
    }

    private static Guid? ResolvePackageOrderId(VodaPayWebhookRequest request)
    {
        var candidates = new[]
        {
            request.EchoData,
            request.PaymentReference,
            request.TransactionId,
            request.PaymentToken,
            request.SessionId,
            request.RetrievalReferenceNumberExtended,
            request.RetrievalReferenceNumber
        };

        foreach (var candidate in candidates)
        {
            if (TryExtractGuid(candidate, out var id))
            {
                return id;
            }
        }

        return null;
    }

    private static VodaPayWebhookRequest ParseVodaPayWebhookRequest(JsonElement payload)
    {
        return new VodaPayWebhookRequest
        {
            EchoData = FindFirstString(payload, "echoData"),
            TransmissionDateTime = FindFirstDateTimeOffset(payload, "transmissionDateTime"),
            PaymentToken = FindFirstString(payload, "paymentToken"),
            PaymentReference = FindFirstString(payload, "paymentReference", "merchantTransactionId", "transactionReference"),
            SessionId = FindFirstString(payload, "sessionId"),
            ResponseCode = FindFirstString(payload, "responseCode", "statusCode"),
            ResponseMessage = FindFirstString(payload, "responseMessage", "statusMessage", "message"),
            RetrievalReferenceNumber = FindFirstString(payload, "retrievalReferenceNumber", "rrn"),
            RetrievalReferenceNumberExtended = FindFirstString(payload, "retrievalReferenceNumberExtended"),
            MerchantId = FindFirstString(payload, "merchantId"),
            MerchantName = FindFirstString(payload, "merchantName"),
            TransactionAmount = FindFirstDecimal(payload, "transactionAmount", "amount"),
            CurrencyCode = FindFirstString(payload, "currencyCode", "currency"),
            TransactionId = FindFirstString(payload, "transactionId"),
            TransactionInfo = FindFirstStringDictionary(payload, "transactionInfo")
        };
    }

    private static string? ResolvePaymentReference(VodaPayWebhookRequest request)
    {
        return request.PaymentReference
            ?? request.TransactionId
            ?? request.PaymentToken
            ?? request.RetrievalReferenceNumberExtended
            ?? request.RetrievalReferenceNumber;
    }

    private static string ResolvePaymentStatus(VodaPayWebhookRequest request)
    {
        var responseCode = request.ResponseCode?.Trim() ?? string.Empty;
        return MapVodaPayFinalStatus(responseCode);
    }

    private static string MapVodaPayFinalStatus(string? responseCode)
    {
        return (responseCode ?? string.Empty).Trim() switch
        {
            "00" or "0" => "paid",
            "17" => "failed",
            "" => "pending",
            _ => "failed"
        };
    }

    private static string ResolveVodaPayCallbackStatus(VodaPayCallbackPayload payload, IReadOnlyDictionary<string, string> queryParameters)
    {
        if (!string.IsNullOrWhiteSpace(payload.ResponseCode))
        {
            return MapVodaPayFinalStatus(payload.ResponseCode);
        }

        var statusSignals = new[]
        {
            TryGetQueryValue(queryParameters, "status"),
            TryGetQueryValue(queryParameters, "resultStatus"),
            TryGetQueryValue(queryParameters, "paymentStatus"),
            TryGetQueryValue(queryParameters, "transactionStatus")
        }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

        if (statusSignals.Any(ContainsFailureKeyword))
        {
            return "failed";
        }

        if (statusSignals.Any(ContainsSuccessKeyword))
        {
            return "paid";
        }

        if (ContainsFailureKeyword(payload.ResponseMessage)
            || ContainsFailureKeyword(TryGetQueryValue(queryParameters, "responseMessage"))
            || ContainsFailureKeyword(TryGetQueryValue(queryParameters, "message")))
        {
            return "failed";
        }

        return "pending";
    }

    private static string? TryGetQueryValue(IReadOnlyDictionary<string, string> queryParameters, string key)
    {
        foreach (var pair in queryParameters)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value?.Trim();
            }
        }

        return null;
    }

    private static bool ContainsFailureKeyword(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Contains("cancel")
            || normalized.Contains("abort")
            || normalized.Contains("declin")
            || normalized.Contains("reject")
            || normalized.Contains("denied")
            || normalized.Contains("fail")
            || normalized.Contains("error")
            || normalized.Contains("timeout");
    }

    private static bool ContainsSuccessKeyword(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Contains("success")
            || normalized.Contains("paid")
            || normalized.Contains("approved")
            || normalized.Contains("complete");
    }

    private static VodaPayCallbackPayload ResolveCallbackPayload(IReadOnlyDictionary<string, string> queryParameters)
    {
        var responseCode = queryParameters.TryGetValue("responseCode", out var directResponseCode)
            ? directResponseCode?.Trim()
            : null;
        var responseMessage = queryParameters.TryGetValue("responseMessage", out var directResponseMessage)
            ? directResponseMessage?.Trim()
            : null;
        var paymentReference = queryParameters.TryGetValue("paymentReference", out var directPaymentReference)
            ? directPaymentReference
            : queryParameters.TryGetValue("transactionId", out var directTransactionId)
                ? directTransactionId
                : null;

        if (!string.IsNullOrWhiteSpace(responseCode))
        {
            return new VodaPayCallbackPayload(responseCode, responseMessage, paymentReference);
        }

        if (!queryParameters.TryGetValue("data", out var encodedData) || string.IsNullOrWhiteSpace(encodedData))
        {
            return new VodaPayCallbackPayload(null, null, paymentReference);
        }

        try
        {
            var decodedBytes = Convert.FromBase64String(AddBase64Padding(encodedData.Trim()));
            var decodedJson = Encoding.UTF8.GetString(decodedBytes);
            using var json = JsonDocument.Parse(decodedJson);
            var root = json.RootElement;

            return new VodaPayCallbackPayload(
                GetJsonString(root, "responseCode"),
                GetJsonString(root, "responseMessage"),
                GetJsonString(root, "paymentReference")
                    ?? GetJsonString(root, "transactionId")
                    ?? paymentReference);
        }
        catch
        {
            return new VodaPayCallbackPayload(null, null, paymentReference);
        }
    }

    private static string AddBase64Padding(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        var padding = padded.Length % 4;
        return padding == 0 ? padded : padded.PadRight(padded.Length + (4 - padding), '=');
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static string? FindFirstString(JsonElement element, params string[] propertyNames)
    {
        var match = FindFirstPropertyValue(element, propertyNames);
        if (match is null)
        {
            return null;
        }

        var value = match.Value.Value;
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static DateTimeOffset? FindFirstDateTimeOffset(JsonElement element, params string[] propertyNames)
    {
        var value = FindFirstString(element, propertyNames);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static decimal? FindFirstDecimal(JsonElement element, params string[] propertyNames)
    {
        var match = FindFirstPropertyValue(element, propertyNames);
        if (match is null)
        {
            return null;
        }

        var value = match.Value.Value;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.String when decimal.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null
        };
    }

    private static Dictionary<string, string?>? FindFirstStringDictionary(JsonElement element, params string[] propertyNames)
    {
        var match = FindFirstPropertyValue(element, propertyNames);
        if (match is null)
        {
            return null;
        }

        var value = match.Value.Value;
        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            dictionary[property.Name] = property.Value.ValueKind == JsonValueKind.Null
                ? null
                : property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.ToString();
        }

        return dictionary;
    }

    private static KeyValuePair<string, JsonElement>? FindFirstPropertyValue(JsonElement element, params string[] propertyNames)
    {
        if (propertyNames.Length == 0)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = element.EnumerateObject().ToArray();
            foreach (var propertyName in propertyNames)
            {
                var exactMatch = properties.FirstOrDefault(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exactMatch.Name))
                {
                    return new KeyValuePair<string, JsonElement>(exactMatch.Name, exactMatch.Value);
                }
            }

            foreach (var property in properties)
            {
                var nested = FindFirstPropertyValue(property.Value, propertyNames);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindFirstPropertyValue(item, propertyNames);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static bool TryExtractGuid(string? input, out Guid id)
    {
        id = Guid.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var directMatch = Regex.Match(input, "[0-9a-fA-F]{8}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{12}");
        if (directMatch.Success && Guid.TryParse(directMatch.Value, out id))
        {
            return true;
        }

        foreach (var token in input.Split(new[] { '-', ':', '|', '/', '?', '&', '=' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(token, out id))
            {
                return true;
            }
        }

        return Guid.TryParse(input, out id);
    }

    private sealed record VodaPayCallbackPayload(string? ResponseCode, string? ResponseMessage, string? PaymentReference);
}
