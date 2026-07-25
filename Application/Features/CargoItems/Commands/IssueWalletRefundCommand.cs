using Application.Common.Interfaces;
using Application.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.CargoItems.Commands;

public record IssueWalletRefundCommand(
    Guid CustomerId,
    Guid CargoItemId,
    decimal RefundAmount,
    string? Currency,
    string Reason
) : IRequest<CargoItemDto>;

public class IssueWalletRefundCommandHandler : IRequestHandler<IssueWalletRefundCommand, CargoItemDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IOmniChannelNotificationService _notificationService;

    public IssueWalletRefundCommandHandler(IApplicationDbContext context, IOmniChannelNotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<CargoItemDto> Handle(IssueWalletRefundCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new ArgumentException($"Customer with ID {request.CustomerId} not found.");

        var cargoItem = await _context.CargoItems
            .FirstOrDefaultAsync(c => c.Id == request.CargoItemId, cancellationToken)
            ?? throw new ArgumentException($"Cargo item with ID {request.CargoItemId} not found.");

        // Security: verify ownership
        if (cargoItem.CustomerId != customer.Id)
            throw new InvalidOperationException("Customer does not own this cargo item.");

        // Business rule: delivered items cannot be refunded automatically
        if (cargoItem.Status == CargoStatus.Delivered)
            throw new InvalidOperationException("Delivered cargo items are locked against automated refund protocols.");

        // Credit wallet — no external cash payouts
        var currency = request.Currency ?? "USD";
        customer.CreditWallet(request.RefundAmount);

        var ledgerEntry = new WalletLedger(
            Guid.NewGuid(),
            customer.Id,
            cargoItem.Id,
            request.RefundAmount,
            currency,
            LedgerTransactionType.Refund,
            $"Logistics refund issued. Reason: {request.Reason}"
        );

        cargoItem.UpdateStatus(CargoStatus.Cancelled);

        _context.WalletLedgers.Add(ledgerEntry);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _notificationService.SendCargoStatusUpdateAsync(
                customer, cargoItem,
                $"Refund of {currency} {request.RefundAmount:F2} issued for {cargoItem.TrackingNumber}.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Refund notification failed silently: {ex.Message}");
        }

        return new CargoItemDto(
            cargoItem.Id, cargoItem.CustomerId, cargoItem.TrackingNumber, cargoItem.MarkerCode,
            cargoItem.Description, cargoItem.ActualWeightKg, cargoItem.LengthCm, cargoItem.WidthCm,
            cargoItem.HeightCm, cargoItem.VolumetricWeightKg, cargoItem.BillableWeightKg,
            cargoItem.DeclaredValueAmount, cargoItem.DeclaredValueCurrency, cargoItem.CustomsCategory,
            cargoItem.Status, cargoItem.ShippingCostAmount, cargoItem.ShippingCostCurrency,
            cargoItem.BatchId, cargoItem.InvoiceId, cargoItem.ShipmentMovementId,
            cargoItem.ShipmentContainerId, cargoItem.AssignedTransportMode, cargoItem.CreatedAt
        );
    }
}
