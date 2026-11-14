using ProjectK.Common.Records.AuthModule;

namespace ProjectK.Common.Contracts.MemberModule;

/// <summary>
/// What the kurin module will tell the access layer about who holds what in it. An office is a fact
/// about a kurin, so the kurin answers for it — and answers for one kurin at a time, which is what
/// keeps a виховник's rights from following them into another.
/// <para>
/// The account is named by its own key, never by the person behind it: this read goes through
/// memberships and offices and never touches the member table.
/// </para>
/// </summary>
public interface IOfficeDirectory
{
    /// <summary>
    /// The offices this account currently holds inside one kurin. An office of another kurin, or one
    /// held under a membership that has since ended, is not among them.
    /// </summary>
    Task<IReadOnlyCollection<MemberOffice>> GetForAccountInKurinAsync(
        Guid userKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default);
}
