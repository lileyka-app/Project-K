using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Records.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Models;

/// <summary>The accounts to list, and the live sessions behind them to write back to the cookie.</summary>
public sealed record SavedAccounts(IReadOnlyList<SavedAccountDto> Accounts, IReadOnlyList<SavedSession> Saved);
