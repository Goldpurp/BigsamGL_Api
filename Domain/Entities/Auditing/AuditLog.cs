using Domain.Common;

namespace Domain.Entities;

public class AuditLog : BaseEntity
{
    public string ActorId { get; set; } = "system";
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string ChangesJson { get; set; } = "{}";
    public string? IpAddress { get; set; }
}
