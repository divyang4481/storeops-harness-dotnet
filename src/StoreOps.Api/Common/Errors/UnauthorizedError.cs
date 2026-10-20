namespace StoreOps.Api.Common.Errors;

/// <summary>Raised when the caller identity (X-Staff-Id header) is missing, unknown or inactive.</summary>
public sealed class UnauthorizedError : AppError
{
    public UnauthorizedError(string message)
        : base(ErrorCodes.Unauthorized, message, StatusCodes.Status401Unauthorized)
    {
    }
}
