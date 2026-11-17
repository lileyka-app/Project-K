using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Records.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Models;

/// <summary>The accounts to list, and the live sessions behind them to write back to the cookie.</summary>
public sealed record SavedAccounts(IReadOnlyList<SavedAccountDto> Accounts, IReadOnlyList<SavedSession> Saved);
