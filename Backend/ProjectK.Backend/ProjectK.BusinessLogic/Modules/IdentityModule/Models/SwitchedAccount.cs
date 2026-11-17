using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Records.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Models;

/// <summary>
/// The account switched to, and the sessions the browser keeps for the others — the one it just
/// left among them. The controller writes both cookies; only <see cref="Login"/> reaches the body.
/// </summary>
public sealed record SwitchedAccount(LoginUserDto Login, IReadOnlyList<SavedSession> Saved);
