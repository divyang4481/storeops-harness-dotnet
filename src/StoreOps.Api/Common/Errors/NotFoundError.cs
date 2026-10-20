namespace StoreOps.Api.Common.Errors;

public sealed class NotFoundError : AppError
{
    public NotFoundError(string resource, string id)
        : base(ErrorCodes.NotFound, $"{resource} '{id}' was not found.", StatusCodes.Status404NotFound)
    {
        Resource = resource;
        ResourceId = id;
    }

    public string Resource { get; }

    public string ResourceId { get; }
}
