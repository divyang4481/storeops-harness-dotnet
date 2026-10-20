namespace StoreOps.Api.Common.Errors;

// Wire format for every error: { "error": { "code", "message", "details" } }.
public sealed record ErrorResponse(ErrorBody Error)
{
    public static ErrorResponse From(AppError error) =>
        new(new ErrorBody(error.Code, error.Message, error.Details));
}
