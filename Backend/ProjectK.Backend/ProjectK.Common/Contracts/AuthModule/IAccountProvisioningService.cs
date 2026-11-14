using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.Common.Contracts.AuthModule;

/// <summary>
/// Issues the pending account an invited person signs in with: the <c>AppUser</c> and the invitation
/// that carries its token. Both ways in go through here — a провід adding a member and an approved
/// waitlist entry — so the account shape and the invitation lifetime are decided once.
/// <para>
/// The account is persisted immediately (Identity writes through its own store); the invitation is
/// only added to the unit of work, so the caller commits it together with the rest of its use case.
/// </para>
/// </summary>
public interface IAccountProvisioningService
{
    /// <summary>Whether an account can still be issued for this address, and what holds it if not.</summary>
    Task<AccountAvailability> CheckAvailabilityAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the pending account and its invitation. Failure carries the identity store's own
    /// reason, so the caller can decide between reporting it and treating it as a broken invariant.
    /// </summary>
    Task<ServiceResult<AccountProvisioningResult>> ProvisionAsync(
        AccountProvisioningRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The account as it stands now, or <c>null</c> when there is none under this key.</summary>
    Task<AccountSnapshot?> FindAsync(
        Guid userKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a fresh invitation for an account nobody has claimed yet, first moving the account to
    /// <paramref name="email"/> when it was opened for another address. Every invitation issued before
    /// is revoked, so a letter that went to a mistyped address stops working. Refuses with
    /// <c>Conflict</c> an account that is already active, or an address someone else holds.
    /// <para>
    /// A new address is persisted immediately; the invitation is only added to the unit of work.
    /// </para>
    /// </summary>
    Task<ServiceResult<AccountProvisioningResult>> ReissueInvitationAsync(
        Guid userKey,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an account that is already in use to another address and ends every session it holds.
    /// This is an admin correcting a record, not the owner's own change, so no confirmation letter
    /// is involved. Persisted immediately.
    /// </summary>
    Task<ServiceResult<Guid>> ChangeEmailAsync(
        Guid userKey,
        string email,
        CancellationToken cancellationToken = default);
}
