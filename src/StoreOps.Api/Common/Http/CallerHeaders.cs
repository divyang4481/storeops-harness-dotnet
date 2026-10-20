namespace StoreOps.Api.Common.Http;

/// <summary>
/// Stub authentication for the capstone: the caller identifies itself with the <c>X-Staff-Id</c> header.
/// A production deployment would replace this with JWT / Entra ID / Cognito claims mapped to the same staff id.
/// </summary>
public static class CallerHeaders
{
    public const string StaffId = "X-Staff-Id";
}
