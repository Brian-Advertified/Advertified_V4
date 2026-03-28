using System;

namespace Advertified.App.Data.Entities;

public partial class EmailTemplate
{
    public Guid Id { get; set; }

    public string TemplateName { get; set; } = null!;

    public string SubjectTemplate { get; set; } = null!;

    public string BodyHtmlTemplate { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
