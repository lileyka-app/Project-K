using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Models;

/// <summary>
/// The account switched to, and the sessions the browser keeps for the others — the one it just
/// left among them. The controller writes both cookies; only <see cref="Login"/> reaches the body.
/// </summary>
public sealed record SwitchedAccount(LoginUserDto Login, IReadOnlyList<SavedSession> Saved);

/// <summary>The accounts to list, and the live sessions behind them to write back to the cookie.</summary>
public sealed record SavedAccounts(IReadOnlyList<SavedAccountDto> Accounts, IReadOnlyList<SavedSession> Saved);
