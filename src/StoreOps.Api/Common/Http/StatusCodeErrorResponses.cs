using Microsoft.AspNetCore.Diagnostics;
using StoreOps.Api.Common.Errors;

namespace StoreOps.Api.Common.Http;

/// <summary>
/// Gives body-less framework responses (unmatched route, wrong verb) the same error shape as <see cref="AppError"/>.
/// </summary>
public static class StatusCodeErrorResponses
{
    public static Task WriteAsync(StatusCodeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var response = context.HttpContext.Response;
        var (code, message) = response.StatusCode switch
        {
            StatusCodes.Status404NotFound => (ErrorCodes.NotFound, "The requested route does not exist."),
            StatusCodes.Status405MethodNotAllowed => (ErrorCodes.MethodNotAllowed, "The HTTP method is not allowed for this route."),
            _ => ($"HTTP_{response.StatusCode}", "The request could not be completed."),
        };

        return response.WriteAsJsonAsync(new ErrorResponse(new ErrorBody(code, message, null)), context.HttpContext.RequestAborted);
    }
}
