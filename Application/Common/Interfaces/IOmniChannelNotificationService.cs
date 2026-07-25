using Domain.Entities;

namespace Application.Common.Interfaces;

public interface IOmniChannelNotificationService
{
    Task SendCargoStatusUpdateAsync(Customer customer, CargoItem cargoItem, string? messageOverride = null, CancellationToken cancellationToken = default);
    Task SendLowBalanceAlertAsync(Customer customer, decimal requiredAmount, CancellationToken cancellationToken = default);
    Task SendBatchDispatchAlertAsync(List<Customer> customers, ShippingBatch batch, CancellationToken cancellationToken = default);
}
