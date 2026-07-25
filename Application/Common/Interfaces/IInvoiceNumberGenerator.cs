namespace Application.Common.Interfaces;

public interface IInvoiceNumberGenerator
{
    Task<string> NextAsync(CancellationToken cancellationToken);
}
