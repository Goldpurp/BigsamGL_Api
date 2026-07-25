using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "StaffOnly")]
[Route("api/communications")]
public sealed class CommunicationsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IClientCommunicationService _communications;
    public CommunicationsController(IApplicationDbContext context, IClientCommunicationService communications)
    {
        _context = context; _communications = communications;
    }

    [HttpPost("clients/{customerId:guid}/email")]
    public async Task<ActionResult> SendGeneralEmail(Guid customerId, SendClientEmailRequest request, CancellationToken cancellationToken)
    {
        var message = await _communications.SendAsync(customerId, CommunicationType.General, request.Subject, request.Body,
            "General", null, request.AttachmentDocumentIds, new { }, cancellationToken);
        return Ok(new { message.Id, message.Status, message.RecipientEmail, message.SentAt, Accepted = true });
    }

    [HttpGet("clients/{customerId:guid}")]
    public async Task<ActionResult> GetClientHistory(Guid customerId, [FromQuery] bool paged = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var ordered = _context.CommunicationMessages.AsNoTracking().Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
        PagedResponse<CommunicationMessage>? pageResult = null;
        IReadOnlyList<CommunicationMessage> messages;
        if (paged)
        {
            pageResult = await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken);
            messages = pageResult.Items;
        }
        else messages = await ordered.Take(200).ToListAsync(cancellationToken);
        var ids = messages.Select(x => x.Id).ToList();
        var attachments = await _context.CommunicationAttachments.AsNoTracking().Where(x => ids.Contains(x.CommunicationMessageId)).ToListAsync(cancellationToken);
        var items = messages.Select(x => new { Message = x,
            AttachmentDocumentIds = attachments.Where(a => a.CommunicationMessageId == x.Id)
                .Select(a => a.ShipmentDocumentId).ToList() }).ToList();
        return pageResult is null ? Ok(items) : Ok(new { items, pageResult.Page, pageResult.PageSize,
            pageResult.HasPrevious, pageResult.HasNext, pageResult.Total, pageResult.TotalPages });
    }
}
