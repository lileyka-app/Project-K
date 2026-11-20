namespace ProjectK.Common.Dtos.IdentityModule;

/// <summary>
/// The kurin an admin opened and the account its Зв'язковий will claim. <see cref="InvitationSent"/>
/// is false when both were stored but the letter did not go; it can be sent again by address
/// (<c>invitation/resend</c>, or «Забув пароль»).
/// </summary>
public sealed record KurinFounderInvitationDto(Guid KurinKey, Guid UserKey, string Email, bool InvitationSent);
