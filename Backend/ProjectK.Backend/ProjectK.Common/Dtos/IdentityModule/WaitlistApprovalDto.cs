namespace ProjectK.Common.Dtos.IdentityModule;

/// <summary>
/// The invitation an approved application got. <see cref="InvitationSent"/> is false when the
/// approval and the account were stored but the letter did not go; it is sent again from the entry.
/// </summary>
public sealed record WaitlistApprovalDto(Guid InvitationKey, bool InvitationSent);
