namespace Advertified.App.Contracts.Leads;

public sealed class SignalDto
{
    public int Id { get; init; }

    public int LeadId { get; init; }

    public bool HasPromo { get; init; }

    public bool HasMetaAds { get; init; }

    public bool WebsiteUpdatedRecently { get; init; }

    public DateTime CreatedAt { get; init; }
}
