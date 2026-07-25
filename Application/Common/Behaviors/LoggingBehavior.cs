using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Logistics System Request: {Name} Handler started processing.", requestName);

        TResponse response;
        try
        {
            response = await next();
            _logger.LogInformation("Logistics System Request: {Name} processed successfully.", requestName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logistics System Request: {Name} failed with exception: {Message}", requestName, ex.Message);
            throw;
        }

        return response;
    }
}
