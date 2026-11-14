namespace ProjectK.Common.Records.AuthModule;

/// <summary>
/// A live session this browser keeps for an account it is not using right now, so the person can
/// switch back to it without signing in again. The token never leaves the httpOnly cookie it came in.
/// </summary>
public sealed record SavedSession(string Token, Guid UserKey, DateTime ExpiresAtUtc);
