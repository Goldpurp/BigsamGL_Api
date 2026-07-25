using Domain.Common;

namespace Domain.Entities;

public class CommunicationAttachment : BaseEntity
{
    public Guid CommunicationMessageId { get; set; }
    public Guid ShipmentDocumentId { get; set; }
}
