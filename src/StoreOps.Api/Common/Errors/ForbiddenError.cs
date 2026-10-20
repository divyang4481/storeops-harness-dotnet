namespace StoreOps.Api.Common.Errors;

/// <summary>Raised when an authenticated caller is not allowed to perform the operation (for example deleting another person's activity).</summary>
public sealed class ForbiddenError : AppError
{
    public ForbiddenError(string message)
        : base(ErrorCodes.Forbidden, message, StatusCodes.Status403Forbidden)
    {
    }
}
