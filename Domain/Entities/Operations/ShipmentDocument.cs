using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class ShipmentDocument : BaseEntity
{
    public Guid? CustomerId { get; set; }
    public Guid? CargoItemId { get; set; }
    public DocumentType Type { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
}
