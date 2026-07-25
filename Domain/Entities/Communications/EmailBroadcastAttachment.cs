using Domain.Common;

namespace Domain.Entities;

public sealed class EmailBroadcastAttachment : BaseEntity
{
    public Guid EmailBroadcastId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
