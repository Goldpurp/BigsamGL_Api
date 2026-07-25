using Application.Common.Interfaces;
using Application.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.CargoItems.Queries;

public record SearchCargoItemsQuery(
    DateTime? DateFrom,
    DateTime? DateTo,
    string? BranchHub,
    string? Mode,
    string? SearchQuery,
    Guid? CustomerId,
    Guid? InvoiceId,
    Guid? ShipmentMovementId,
    Guid? ShipmentContainerId,
    CargoStatus? Status,
    bool? IsAllocated,
    int Skip,
    int Take
) : IRequest<List<CargoItemDto>>;

public class SearchCargoItemsQueryHandler : IRequestHandler<SearchCargoItemsQuery, List<CargoItemDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchCargoItemsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CargoItemDto>> Handle(SearchCargoItemsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.CargoItems.AsNoTracking().AsQueryable();

        // Filter: Date range
        if (request.DateFrom.HasValue)
            query = query.Where(c => c.CreatedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(c => c.CreatedAt <= request.DateTo.Value);

        if (request.CustomerId.HasValue) query = query.Where(c => c.CustomerId == request.CustomerId);
        if (request.InvoiceId.HasValue) query = query.Where(c => c.InvoiceId == request.InvoiceId);
        if (request.ShipmentMovementId.HasValue)
            query = query.Where(c => c.ShipmentMovementId == request.ShipmentMovementId);
        if (request.ShipmentContainerId.HasValue)
            query = query.Where(c => c.ShipmentContainerId == request.ShipmentContainerId);
        if (request.Status.HasValue) query = query.Where(c => c.Status == request.Status);
        if (request.IsAllocated.HasValue)
            query = request.IsAllocated.Value
                ? query.Where(c => c.ShipmentMovementId != null)
                : query.Where(c => c.ShipmentMovementId == null);
        if (!string.IsNullOrWhiteSpace(request.Mode) &&
            Enum.TryParse<Domain.Enums.TransportMode>(request.Mode, true, out var parsedMode))
            query = query.Where(c => c.AssignedTransportMode == parsedMode);

        // Filter: Branch Hub — match against batch origin/destination hubs
        if (!string.IsNullOrWhiteSpace(request.BranchHub))
        {
            var hub = request.BranchHub;
            query = query.Where(c => c.BatchId.HasValue && _context.ShippingBatches.Any(b =>
                b.Id == c.BatchId.Value && (b.OriginHub.Contains(hub) || b.DestinationHub.Contains(hub))));
        }

        // Filter: Search query — match name, phone, tracking number, or marker code
        if (!string.IsNullOrWhiteSpace(request.SearchQuery))
        {
            var search = request.SearchQuery.ToLower();
            query = query.Where(c =>
                c.TrackingNumber.ToLower().Contains(search) ||
                c.MarkerCode.ToLower().Contains(search) ||
                c.Description.ToLower().Contains(search) ||
                _context.Customers.Any(customer => customer.Id == c.CustomerId &&
                    (customer.FullName.ToLower().Contains(search) ||
                     customer.Email != null && customer.Email.ToLower().Contains(search)))
            );
        }

        var items = await query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Skip(Math.Max(0, request.Skip)).Take(Math.Clamp(request.Take, 1, 201))
            .ToListAsync(cancellationToken);

        return items.Select(c => new CargoItemDto(
            c.Id, c.CustomerId, c.TrackingNumber, c.MarkerCode,
            c.Description, c.ActualWeightKg, c.LengthCm, c.WidthCm,
            c.HeightCm, c.VolumetricWeightKg, c.BillableWeightKg,
            c.DeclaredValueAmount, c.DeclaredValueCurrency, c.CustomsCategory,
            c.Status, c.ShippingCostAmount, c.ShippingCostCurrency,
            c.BatchId, c.InvoiceId, c.ShipmentMovementId, c.ShipmentContainerId,
            c.AssignedTransportMode, c.CreatedAt
        )).ToList();
    }
}
