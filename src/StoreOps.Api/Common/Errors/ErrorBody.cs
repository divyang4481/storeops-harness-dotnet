namespace StoreOps.Api.Common.Errors;

public sealed record ErrorBody(string Code, string Message, IReadOnlyDictionary<string, string[]>? Details);
