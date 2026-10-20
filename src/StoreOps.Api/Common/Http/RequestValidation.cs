using StoreOps.Api.Common.Errors;

namespace StoreOps.Api.Common.Http;

public static class RequestValidation
{
    /// <summary>Rejects a missing JSON body with a typed error instead of a framework exception.</summary>
    /// <typeparam name="T">The request body type.</typeparam>
    /// <param name="body">The bound body, or <c>null</c> when the request had none.</param>
    /// <returns>The non-null body.</returns>
    public static T RequireBody<T>(T? body)
        where T : class
    {
        return body ?? throw new BadRequestError("Request body is required.");
    }
}
