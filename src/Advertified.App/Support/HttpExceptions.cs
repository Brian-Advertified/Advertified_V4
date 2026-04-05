namespace Advertified.App.Support;

/// <summary>
/// Exception that should result in HTTP 400 Bad Request
/// </summary>
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
    public BadRequestException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception that should result in HTTP 401 Unauthorized
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
    public UnauthorizedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception that should result in HTTP 403 Forbidden
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
    public ForbiddenException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception that should result in HTTP 404 Not Found
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception that should result in HTTP 400 Bad Request when a campaign action
/// depends on payment having been completed first.
/// </summary>
public sealed class PaymentRequiredException : BadRequestException
{
    public PaymentRequiredException(string message) : base(message) { }
    public PaymentRequiredException(string message, Exception innerException) : base(message, innerException) { }
}
