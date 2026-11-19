using FoodExpress.services;

namespace FoodExpress.exception;

/// <summary>Base exception for expected FoodExpress application errors.</summary>
public class FoodExpressException : Exception
{
    public FoodExpressException(string message) : base(message) { }
    public FoodExpressException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Indicates invalid user input or domain data.</summary>
public sealed class ValidationException : FoodExpressException
{
    public ValidationException(string message) : base(message) { }
}

/// <summary>Indicates that an email address is missing or malformed.</summary>
public sealed class InvalidEmailException : FoodExpressException
{
    public InvalidEmailException() : base("Enter a valid email address containing the @ symbol.") { }
}

/// <summary>Indicates authentication failure without revealing the failing field.</summary>
public sealed class InvalidCredentialsException : FoodExpressException
{
    public InvalidCredentialsException() : base("Invalid username or password.") { }
}

/// <summary>Indicates a role or ownership authorization failure.</summary>
public sealed class AuthorizationException : FoodExpressException
{
    public AuthorizationException(string message = "You are not authorized to perform this action.") : base(message) { }
}

/// <summary>Indicates an unavailable menu item.</summary>
public sealed class ItemUnavailableException : FoodExpressException
{
    public ItemUnavailableException(string message) : base(message) { }
}

/// <summary>Indicates that an operation requires a non-empty cart.</summary>
public sealed class EmptyCartException : FoodExpressException
{
    public EmptyCartException() : base("Your cart is empty.") { }
}

/// <summary>Indicates an invalid order state transition.</summary>
public sealed class InvalidOrderStatusTransitionException : FoodExpressException
{
    public InvalidOrderStatusTransitionException(string message) : base(message) { }
}

/// <summary>Indicates an invalid or expired promo code.</summary>
public sealed class PromoCodeException : FoodExpressException
{
    public PromoCodeException(string message) : base(message) { }
}

/// <summary>Indicates that email delivery failed.</summary>
public sealed class EmailDeliveryException : FoodExpressException
{
    public EmailDeliveryException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Indicates a file persistence or parsing failure.</summary>
public sealed class DataFileException : FoodExpressException
{
    public DataFileException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Maps exceptions to user-safe messages while recording diagnostic details.</summary>
public static class ErrorHandler
{
    public static string Handle(Exception exception, LoggerService? logger = null)
    {
        logger?.Log("ERROR", "APPLICATION_ERROR", exception.ToString());
        return exception is FoodExpressException ? exception.Message : "An unexpected error occurred. Please try again.";
    }
}
