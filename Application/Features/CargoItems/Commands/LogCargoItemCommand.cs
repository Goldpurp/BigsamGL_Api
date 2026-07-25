using Application.Common.Interfaces;
using Application.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.CargoItems.Commands;

public record LogCargoItemCommand(
    Guid CustomerId,
    string Description,
    decimal ActualWeightKg,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    decimal DeclaredValue,
    string? Currency,
    string? CustomsCategory
) : IRequest<CargoItemDto>;

public class LogCargoItemCommandHandler : IRequestHandler<LogCargoItemCommand, CargoItemDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IOmniChannelNotificationService _notificationService;

    public LogCargoItemCommandHandler(IApplicationDbContext context, IOmniChannelNotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<CargoItemDto> Handle(LogCargoItemCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve customer
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (customer == null)
        {
            throw new ArgumentException($"Execution halted: Customer with specified ID {request.CustomerId} could not be resolved.");
        }

        // 2. Generate tracking number
        var random = new Random();
        var trackingNumber = $"TRK-BGL-{customer.CountryCode}-{random.Next(100000, 999999)}";
        var cargoId = Guid.NewGuid();

        // 3. Initialize cargo item
        var cargoItem = new CargoItem(
            cargoId,
            customer.Id,
            trackingNumber,
            request.Description,
            request.ActualWeightKg,
            request.LengthCm,
            request.WidthCm,
            request.HeightCm,
            request.DeclaredValue,
            request.Currency ?? "USD",
            request.CustomsCategory ?? "General Cargo",
            customer.CountryCode
        );

        // 4. Calculate pricing rates
        const decimal baseRatePerKg = 4.50m;
        const decimal customsMultiplier = 0.02m;
        decimal calculatedCost = cargoItem.CalculateShippingRates(baseRatePerKg, customsMultiplier);
        cargoItem.ShippingCostAmount = calculatedCost;

        // 5. Auto-Credit Roll-Over Logic
        if (customer.WalletBalance > 0)
        {
            decimal appliedCredit = Math.Min(customer.WalletBalance, calculatedCost);
            customer.DebitWallet(appliedCredit);
            cargoItem.ShippingCostAmount -= appliedCredit;

            if (cargoItem.ShippingCostAmount == 0)
            {
                cargoItem.UpdateStatus(CargoStatus.ReceivedAtWarehouse);
            }

            var ledgerId = Guid.NewGuid();
            var walletLedger = new WalletLedger(
                ledgerId,
                customer.Id,
                cargoItem.Id,
                appliedCredit,
                cargoItem.ShippingCostCurrency,
                LedgerTransactionType.CargoPayment,
                $"Cargo item tracking code {cargoItem.TrackingNumber} upfront logistics fee deduction of {appliedCredit}."
            );

            _context.WalletLedgers.Add(walletLedger);
        }

        _context.CargoItems.Add(cargoItem);
        await _context.SaveChangesAsync(cancellationToken);

        // 6. Send alerts
        try
        {
            await _notificationService.SendCargoStatusUpdateAsync(customer, cargoItem, null, cancellationToken);
            if (cargoItem.ShippingCostAmount > 0)
            {
                await _notificationService.SendLowBalanceAlertAsync(customer, cargoItem.ShippingCostAmount, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Fail silently as per requirement
            Console.WriteLine($"Alerting failed: {ex.Message}");
        }

        return new CargoItemDto(
            cargoItem.Id,
            cargoItem.CustomerId,
            cargoItem.TrackingNumber,
            cargoItem.MarkerCode,
            cargoItem.Description,
            cargoItem.ActualWeightKg,
            cargoItem.LengthCm,
            cargoItem.WidthCm,
            cargoItem.HeightCm,
            cargoItem.VolumetricWeightKg,
            cargoItem.BillableWeightKg,
            cargoItem.DeclaredValueAmount,
            cargoItem.DeclaredValueCurrency,
            cargoItem.CustomsCategory,
            cargoItem.Status,
            cargoItem.ShippingCostAmount,
            cargoItem.ShippingCostCurrency,
            cargoItem.BatchId,
            cargoItem.InvoiceId,
            cargoItem.ShipmentMovementId,
            cargoItem.ShipmentContainerId,
            cargoItem.AssignedTransportMode,
            cargoItem.CreatedAt
        );
    }
}
