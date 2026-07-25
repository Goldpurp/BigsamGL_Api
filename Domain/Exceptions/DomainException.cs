namespace Domain.Exceptions;

/// <summary>
/// Core base class representing custom exceptions raised inside Domain Layer business policies.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) 
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
