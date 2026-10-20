namespace StoreOps.Api.Common.Errors;

public sealed class ValidationError : AppError
{
    private readonly Dictionary<string, string[]> _errors;

    public ValidationError(string message, IDictionary<string, string[]>? errors = null)
        : base(ErrorCodes.ValidationFailed, message, StatusCodes.Status400BadRequest)
    {
        _errors = errors is null
            ? new Dictionary<string, string[]>(StringComparer.Ordinal)
            : new Dictionary<string, string[]>(errors, StringComparer.Ordinal);
    }

    public override IReadOnlyDictionary<string, string[]>? Details => _errors.Count == 0 ? null : _errors;

    public static ValidationError ForField(string field, string message) =>
        new(message, new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = new[] { message } });
}
