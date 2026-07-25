using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class ClientCommunicationService : IClientCommunicationService
{
    private readonly IApplicationDbContext _context;
    private readonly IIntegrationGateway _gateway;

    public ClientCommunicationService(IApplicationDbContext context, IIntegrationGateway gateway)
    {
        _context = context; _gateway = gateway;
    }

    public async Task<CommunicationMessage> SendAsync(Guid customerId, CommunicationType type, string subject, string body,
        string referenceType, Guid? referenceId, IReadOnlyCollection<Guid> attachmentDocumentIds,
        object documentPayload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 250) throw new ArgumentException("Email subject is required and cannot exceed 250 characters.");
        if (string.IsNullOrWhiteSpace(body) || body.Length > 10_000) throw new ArgumentException("Email body is required and cannot exceed 10,000 characters.");
        if (attachmentDocumentIds.Count > 10) throw new ArgumentException("A maximum of 10 attachments is allowed.");
        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("Client not found.");
        var recipient = customer.Email?.Trim();
        if (string.IsNullOrWhiteSpace(recipient))
            throw new InvalidOperationException("The selected client has no email address.");
        var ids = attachmentDocumentIds.Distinct().ToList();
        var documents = await _context.ShipmentDocuments.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        if (documents.Count != ids.Count) throw new ArgumentException("One or more attachments do not exist.");
        var cargoIds = await _context.CargoItems.AsNoTracking().Where(x => x.CustomerId == customerId).Select(x => x.Id).ToListAsync(cancellationToken);
        if (documents.Any(x => x.CustomerId != customerId && (!x.CargoItemId.HasValue || !cargoIds.Contains(x.CargoItemId.Value))))
            throw new UnauthorizedAccessException("An attachment does not belong to this client.");
        if (documents.Any(x => !x.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(x.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Email attachments must be images or PDF documents.");
        if (documents.Sum(x => x.SizeBytes) > 25_000_000) throw new ArgumentException("Combined attachment size cannot exceed 25 MB.");

        var message = new CommunicationMessage { CustomerId = customerId, Type = type, RecipientEmail = recipient,
            Subject = subject.Trim(), Body = body.Trim(), ReferenceType = referenceType.Trim(), ReferenceId = referenceId,
            Status = CommunicationStatus.Queued, ProviderReference = $"delivery-{Guid.NewGuid():N}" };
        _context.CommunicationMessages.Add(message);
        foreach (var document in documents)
            _context.CommunicationAttachments.Add(new CommunicationAttachment { CommunicationMessageId = message.Id, ShipmentDocumentId = document.Id });

        await _gateway.SendNotificationAsync(IntegrationKind.Email, recipient, "client-document-email", new
        {
            message.Subject, message.Body, Reference = new { Type = referenceType, Id = referenceId }, Document = documentPayload,
            Attachments = documents.Select(x => new { x.FileName, x.ContentType, x.SizeBytes, x.StorageKey, x.Checksum })
        }, cancellationToken);
        message.Status = CommunicationStatus.Sent; message.SentAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken); return message;
    }
}
