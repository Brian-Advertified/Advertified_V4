namespace Advertified.App.Configuration;

public sealed class VodaPayOptions
{
    public const string SectionName = "VodaPay";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool IsTest { get; set; }
    public string InitiatePath { get; set; } = "/Pay/OnceOff";
    public decimal VatRatePercent { get; set; } = 15m;
    public Guid DigitalWalletId { get; set; }
    public string AdditionalDataPrefix { get; set; } = "OPCD";
    public string NotificationUrl { get; set; } = string.Empty;
    public VodaPayStylingOptions Styling { get; set; } = new();
    public VodaPayElectronicReceiptOptions ElectronicReceipt { get; set; } = new();
    public VodaPayCommunicationOptions Communication { get; set; } = new();
}

public sealed class VodaPayStylingOptions
{
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public int? Theme { get; set; }
}

public sealed class VodaPayElectronicReceiptOptions
{
    public int? Method { get; set; }
}

public sealed class VodaPayCommunicationOptions
{
    public string? Message { get; set; }
}
