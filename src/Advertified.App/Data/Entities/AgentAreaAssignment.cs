using System;

namespace Advertified.App.Data.Entities;

public sealed class AgentAreaAssignment
{
    public Guid Id { get; set; }
    public Guid AgentUserId { get; set; }
    public string AreaCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public UserAccount AgentUser { get; set; } = null!;
}
