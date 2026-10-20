namespace StoreOps.Api.Common.Errors;

public sealed class BadRequestError : AppError
{
    public BadRequestError(string message)
        : base(ErrorCodes.BadRequest, message, StatusCodes.Status400BadRequest)
    {
    }
}
