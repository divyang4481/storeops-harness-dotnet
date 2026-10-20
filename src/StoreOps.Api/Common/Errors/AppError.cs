namespace StoreOps.Api.Common.Errors;

/// <summary>
/// Base type for every error raised by services and routes. Carries a stable machine-readable
/// <see cref="Code"/>, a human-readable message and the HTTP status code it maps to.
/// Services and routes throw only <see cref="AppError"/> subtypes, never raw exceptions.
/// </summary>
public abstract class AppError : Exception
{
    protected AppError(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }

    /// <summary>Gets optional structured details (for example, per-field validation messages).</summary>
    public virtual IReadOnlyDictionary<string, string[]>? Details => null;
}
