using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class ResendEmailService : ITemplatedEmailService
{
    private const string ProviderKey = "resend";
    private readonly AppDbContext _db;
    private readonly ResendOptions _options;
    private readonly IEmailDeliveryTrackingService _trackingService;

    public ResendEmailService(
        AppDbContext db,
        IOptions<ResendOptions> options,
        IEmailDeliveryTrackingService trackingService)
    {
        _db = db;
        _options = options.Value;
        _trackingService = trackingService;
    }

    public async Task SendAsync(
        string templateName,
        string recipientEmail,
        string senderKey,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlyCollection<EmailAttachment>? attachments,
        EmailTrackingContext? trackingContext,
        CancellationToken cancellationToken)
    {
        var template = await _db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TemplateName == templateName && x.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException($"Email template '{templateName}' was not found.");

        var fromAddress = ResolveFromAddress(senderKey);
        var subject = Render(template.SubjectTemplate, tokens);
        var html = Render(template.BodyHtmlTemplate, tokens);
        await _trackingService.CreatePendingDispatchAsync(
            ProviderKey,
            templateName,
            senderKey,
            fromAddress,
            recipientEmail,
            subject,
            html,
            attachments,
            trackingContext,
            cancellationToken);
    }

    private string ResolveFromAddress(string senderKey)
    {
        if (_options.SenderAddresses.TryGetValue(senderKey, out var senderAddress) &&
            !string.IsNullOrWhiteSpace(senderAddress))
        {
            return senderAddress;
        }

        if (!string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            return _options.FromEmail;
        }

        throw new InvalidOperationException($"No sender address is configured for key '{senderKey}'.");
    }

    private static string Render(string template, IReadOnlyDictionary<string, string?> tokens)
    {
        var rendered = template;

        foreach (var token in tokens)
        {
            rendered = rendered.Replace(
                "{{" + token.Key + "}}",
                token.Value ?? string.Empty,
                StringComparison.Ordinal);
        }

        return rendered;
    }
}
