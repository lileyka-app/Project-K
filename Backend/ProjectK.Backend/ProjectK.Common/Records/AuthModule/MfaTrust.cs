namespace ProjectK.Common.Records.AuthModule;

/// <summary>A trust token read back: whose it is, and the stamp the account had when it was issued.</summary>
public sealed record MfaTrust(Guid UserId, string SecurityStamp);
