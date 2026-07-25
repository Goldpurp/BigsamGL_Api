using Domain.Entities;
using Domain.Enums;

namespace Application.Common.Interfaces;

public interface IClientCommunicationService
{
    Task<CommunicationMessage> SendAsync(Guid customerId, CommunicationType type, string subject, string body,
        string referenceType, Guid? referenceId, IReadOnlyCollection<Guid> attachmentDocumentIds,
        object documentPayload, CancellationToken cancellationToken);
}
