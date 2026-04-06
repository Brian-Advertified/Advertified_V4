using System;

namespace Advertified.App.Data.Entities;

public partial class FormOptionItem
{
    public Guid Id { get; set; }

    public string OptionSetKey { get; set; } = null!;

    public string Value { get; set; } = null!;

    public string Label { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
