namespace YourOrderWeDeliver.Core.Common;

/// <summary>
/// Raised when an operation would break a business rule or receives invalid input.
/// Messages are written for end users (QR4.5): they name the activity and suggest what to do next.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    /// <summary>Creates an exception with a generic message.</summary>
    public BusinessRuleException()
        : base("The requested activity could not be completed.")
    {
    }

    /// <summary>Creates an exception with a user-facing message.</summary>
    public BusinessRuleException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a user-facing message and the underlying cause.</summary>
    public BusinessRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
