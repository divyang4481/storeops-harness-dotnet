namespace StoreOps.Api.Common.Errors;

public sealed class ConflictError : AppError
{
    public ConflictError(string message)
        : base(ErrorCodes.Conflict, message, StatusCodes.Status409Conflict)
    {
    }
}
