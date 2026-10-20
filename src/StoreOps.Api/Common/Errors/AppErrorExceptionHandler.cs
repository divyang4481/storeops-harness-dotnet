using Microsoft.AspNetCore.Diagnostics;

namespace StoreOps.Api.Common.Errors;

/// <summary>
/// Single translation point from thrown errors to HTTP responses.
/// <see cref="AppError"/> maps to its own status/code; framework binding failures map to
/// <see cref="BadRequestError"/>; anything else becomes a generic <see cref="InternalError"/>.
/// </summary>
public sealed partial class AppErrorExceptionHandler : IExceptionHandler
{
    private readonly ILogger<AppErrorExceptionHandler> _logger;

    public AppErrorExceptionHandler(ILogger<AppErrorExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        AppError error = exception switch
        {
            AppError appError => appError,
            BadHttpRequestException badRequest => new BadRequestError(badRequest.Message),
            _ => new InternalError(),
        };

        if (error is InternalError)
        {
            LogUnhandled(_logger, exception);
        }
        else
        {
            LogHandled(_logger, error.Code, error.StatusCode);
        }

        httpContext.Response.StatusCode = error.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(ErrorResponse.From(error), cancellationToken).ConfigureAwait(false);
        return true;
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Request failed with {Code} ({StatusCode})")]
    private static partial void LogHandled(ILogger logger, string code, int statusCode);
}
