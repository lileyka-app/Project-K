using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.Common.Contracts.MemberModule;

/// <summary>
/// The member module's write door for a roster import: open a record for someone new and open their
/// account. Taking someone into the kurin is the kurin's own command. Each call runs the member
/// module's own use case, so the rules of writing a person stay where they are.
/// <para>
/// A write contract on purpose: a Directory only answers, and an event does not
/// fit. The import needs the new member's key back at once — the account is opened for the person
/// just written — and the whole file lands in one transaction or not at all. A handler reacting to an
/// event after the save could give neither.
/// </para>
/// </summary>
public interface IMemberImportWriter
{
    /// <summary>Opens a member record and places the person. The new member's key; null when it could not be written.</summary>
    Task<Guid?> CreateAsync(ImportedMember person, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an account and sends the activation letter. Conflict when the person already has an
    /// account; the result says whether the letter went out.
    /// </summary>
    Task<ServiceResult<MemberInvitation>> InviteAsync(Guid memberKey, CancellationToken cancellationToken = default);
}
