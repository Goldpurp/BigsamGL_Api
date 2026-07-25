namespace Domain.Exceptions;

/// <summary>
/// Custom domain exceptions thrown when domain operations violate core rules (e.g. over capacity limits).
/// </summary>
public class LogisticsException : DomainException
{
    public LogisticsException(string message) 
        : base(message)
    {
    }

    public LogisticsException(string businessRules, string message) 
        : base($"Rule violation in: {businessRules}. Details: {message}")
    {
    }
}
