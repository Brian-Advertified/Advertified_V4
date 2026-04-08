using System;

namespace Advertified.App.Data.Entities;

public partial class InventoryImportBatch
{
    public Guid Id { get; set; }

    public string ChannelFamily { get; set; } = null!;

    public string SourceType { get; set; } = null!;

    public string SourceIdentifier { get; set; } = null!;

    public string? SourceChecksum { get; set; }

    public int RecordCount { get; set; }

    public string Status { get; set; } = null!;

    public bool IsActive { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ActivatedAt { get; set; }
}
