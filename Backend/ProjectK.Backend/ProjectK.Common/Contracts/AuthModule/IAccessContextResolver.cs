using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Records.AuthModule;

namespace ProjectK.Common.Contracts.AuthModule;

/// <summary>
/// Works out what an account may do in the kurin it is currently in. Every token minted, every
/// "is this person privileged" question and every screen that shows someone's role goes through
/// here, so there is exactly one answer and it is always kurin-aware.
/// <para>
/// It replaces reading identity roles. Those said what a person was <i>anywhere</i>, which stopped
/// being a sensible thing to say the moment a person could belong to two kurins.
/// </para>
/// </summary>
public interface IAccessContextResolver
{
    /// <summary>
    /// The account's roles and scope as things stand. The kurin is the one the account is scoped
    /// into; offices held elsewhere do not count towards it.
    /// </summary>
    Task<AccessContext> ResolveAsync(Guid userKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same for an account already loaded. The sign-in paths have one in hand and would
    /// otherwise fetch it a second time to answer a question they could already answer.
    /// </summary>
    Task<AccessContext> ResolveAsync(AppUser user, CancellationToken cancellationToken = default);
}
