using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Records.AuthModule;

namespace ProjectK.Common.Contracts.AuthModule;

/// <summary>
/// Turns a resolved <see cref="AccessContext"/> into what the clients are told: sign-in and
/// refresh both answer through here, so the two can never disagree about a person's permissions.
/// </summary>
public interface IAccessSnapshotFactory
{
    /// <summary>The roles, kurin and kurin-aware permissions of <paramref name="access"/>.</summary>
    Task<AccessSnapshot> CreateAsync(AccessContext access, CancellationToken cancellationToken = default);
}
