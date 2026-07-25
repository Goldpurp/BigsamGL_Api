using Domain.Common;

namespace Domain.Entities;

/// <summary>Stores the committed result of a protected create or financial command.</summary>
public sealed class IdempotencyRecord : BaseEntity
{
    public Guid ActorUserId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public Guid Key { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public DateTime ExpiresAt { get; set; }
}
