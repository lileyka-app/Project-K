namespace ProjectK.Common.Records.AuthModule;

/// <summary>The account that was created and the invitation that lets its owner claim it.</summary>
public sealed record AccountProvisioningResult(Guid UserKey, Guid InvitationKey, string InvitationToken);
