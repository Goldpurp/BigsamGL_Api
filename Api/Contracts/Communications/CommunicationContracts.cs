namespace Api.Controllers;

public record SendClientEmailRequest(string Subject, string Body, IReadOnlyList<Guid> AttachmentDocumentIds);
public record SendManifestEmailRequest(Guid CustomerId, string Subject, string Body, IReadOnlyList<Guid> AttachmentDocumentIds);
