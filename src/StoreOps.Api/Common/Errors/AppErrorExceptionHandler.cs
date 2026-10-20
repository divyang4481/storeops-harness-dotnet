using System.Text.Json;
using System.Text.RegularExpressions;
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
            BadHttpRequestException badRequest => FromBindingFailure(badRequest),
            _ => new InternalError(),
        };

        if (error is InternalError)
        {
            LogUnhandled(_logger, exception);
        }
        else if (exception is BadHttpRequestException)
        {
            LogBindingFailure(_logger, error.Code, exception.Message);
        }
        else
        {
            LogHandled(_logger, error.Code, error.StatusCode);
        }

        httpContext.Response.StatusCode = error.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(ErrorResponse.From(error), cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Converts a framework binding failure into a typed error without leaking internal type names
    /// (for example <c>Nullable&lt;ActivityStatus&gt;</c>) to clients. The full message is still logged.
    /// </summary>
    /// <param name="exception">The binding failure raised by minimal APIs.</param>
    /// <returns>A client-safe typed error.</returns>
    public static AppError FromBindingFailure(BadHttpRequestException exception)
    {
        if (exception.StatusCode == StatusCodes.Status415UnsupportedMediaType)
        {
            return new UnsupportedMediaTypeError();
        }

        if (exception.InnerException is JsonException)
        {
            return new BadRequestError("Request body is not valid JSON or contains invalid values.");
        }

        var parameter = ParameterName().Match(exception.Message);
        return parameter.Success
            ? new BadRequestError($"Invalid value for parameter '{parameter.Groups["name"].Value}'.")
            : new BadRequestError("The request is invalid.");
    }

    [GeneratedRegex(@"parameter ""(?:[^""]*\s)?(?<name>\w+)""")]
    private static partial Regex ParameterName();

    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Request failed with {Code} ({StatusCode})")]
    private static partial void LogHandled(ILogger logger, string code, int statusCode);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Request binding failed with {Code}: {Detail}")]
    private static partial void LogBindingFailure(ILogger logger, string code, string detail);
}
