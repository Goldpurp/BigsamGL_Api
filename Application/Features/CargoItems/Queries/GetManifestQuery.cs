using Application.Common.Interfaces;
using Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.CargoItems.Queries;

public record GetManifestQuery(Guid BatchId) : IRequest<ManifestResponse>;

public class GetManifestQueryHandler : IRequestHandler<GetManifestQuery, ManifestResponse>
{
    private readonly IApplicationDbContext _context;

    public GetManifestQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ManifestResponse> Handle(GetManifestQuery request, CancellationToken cancellationToken)
    {
        var batch = await _context.ShippingBatches
            .FirstOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken)
            ?? throw new ArgumentException($"Shipping batch with ID {request.BatchId} not found.");

        var cargoItems = await _context.CargoItems
            .Where(c => c.BatchId == request.BatchId)
            .ToListAsync(cancellationToken);

        var movement = await _context.ShipmentMovements.AsNoTracking()
            .Where(x => x.ShippingBatchId == request.BatchId)
            .OrderByDescending(x => x.ScheduledDepartureAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Build manifest line items — excludes all internal financial/pricing data for customs compliance
        var lineItems = cargoItems.Select(c =>
        {
            decimal volumeCbm = Math.Round((c.LengthCm * c.WidthCm * c.HeightCm) / 1000000m, 3);
            decimal verifiedWeight = Math.Max(c.ActualWeightKg, c.VolumetricWeightKg);

            return new ManifestLineItemDto(
                c.MarkerCode,
                $"{batch.OriginHub} → {batch.DestinationHub}",
                1, // Each cargo item row = 1 piece
                verifiedWeight,
                volumeCbm
            );
        }).ToList();

        return new ManifestResponse(
            batch.BatchCode,
            movement?.MasterTransportReference ?? batch.AirwayBillNumber ?? "N/A",
            movement?.VesselName ?? batch.VesselName,
            movement?.VoyageNumber ?? batch.VoyageNumber,
            movement?.OriginGateway ?? batch.PortOfLoading,
            movement?.DestinationGateway ?? batch.PortOfDischarge,
            batch.ScheduledReleaseDate,
            lineItems,
            new ManifestTransportDto(
                "FreightForwarderAndConsolidator",
                movement?.MovementCode,
                movement?.Mode,
                movement?.OperatorType,
                movement?.CarrierName,
                movement?.MasterTransportReference ?? batch.AirwayBillNumber,
                movement?.FlightNumber,
                movement?.VesselName ?? NullIfBlank(batch.VesselName),
                movement?.VoyageNumber ?? NullIfBlank(batch.VoyageNumber),
                movement?.OriginGateway ?? NullIfBlank(batch.PortOfLoading),
                movement?.DestinationGateway ?? NullIfBlank(batch.PortOfDischarge))
        );
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
