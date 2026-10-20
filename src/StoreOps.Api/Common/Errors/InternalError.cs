namespace StoreOps.Api.Common.Errors;

/// <summary>
/// Used by the global handler to represent any unexpected failure without leaking internals.
/// </summary>
public sealed class InternalError : AppError
{
    public InternalError()
        : base(ErrorCodes.Internal, "An unexpected error occurred.", StatusCodes.Status500InternalServerError)
    {
    }
}
