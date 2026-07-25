using Application.DTOs;
using Application.Features.CargoItems.Commands;
using Application.Features.CargoItems.Queries;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Api.Controllers;

[Authorize(Policy = "OperationsTeam")]
public class CargoController : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CargoItemDto>> LogCargo([FromBody] LogCargoItemCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult> SearchCargo(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? branchHub,
        [FromQuery] string? mode,
        [FromQuery] string? searchQuery,
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? invoiceId,
        [FromQuery] Guid? shipmentMovementId,
        [FromQuery] Guid? shipmentContainerId,
        [FromQuery] CargoStatus? status,
        [FromQuery] bool? isAllocated,
        [FromQuery] bool paged = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 100_000); pageSize = Math.Clamp(pageSize, 1, 200);
        var take = paged ? pageSize + 1 : 200;
        var query = new SearchCargoItemsQuery(dateFrom, dateTo, branchHub, mode, searchQuery,
            customerId, invoiceId, shipmentMovementId, shipmentContainerId, status, isAllocated,
            paged ? (page - 1) * pageSize : 0, take);
        var result = await Mediator.Send(query, cancellationToken);
        if (paged)
        {
            var hasNext = result.Count > pageSize;
            if (hasNext) result.RemoveAt(result.Count - 1);
            return Ok(new PagedResponse<CargoItemDto>(result, page, pageSize, page > 1,
                hasNext, null, null));
        }
        return Ok(result);
    }

    [HttpPost("refund")]
    public async Task<ActionResult<CargoItemDto>> RefundCargo([FromBody] IssueWalletRefundCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }

}
