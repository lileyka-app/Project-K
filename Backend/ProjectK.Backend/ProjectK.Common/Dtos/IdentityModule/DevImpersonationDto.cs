namespace ProjectK.Common.Dtos.IdentityModule;

/// <summary>
/// What the dev role switcher gets: a sign-in as the chosen account, plus the ticket that brings
/// the administrator back without a password.
/// </summary>
public sealed record DevImpersonationDto(LoginUserDto Login, string ReturnTicket, string Role, Guid KurinKey);
