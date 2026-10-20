namespace StoreOps.Api.Common.Errors;

public sealed class UnsupportedMediaTypeError : AppError
{
    public UnsupportedMediaTypeError()
        : base(ErrorCodes.UnsupportedMediaType, "Content-Type must be application/json.", StatusCodes.Status415UnsupportedMediaType)
    {
    }
}
