using Moq;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.API.Tests.TestHelpers;

/// <summary>
/// Stored facts a faked <see cref="IResourceScopeRepository"/> hands back, shaped so the real rules
/// give the answer a test wants.
/// </summary>
public static class ScopeFacts
{
    /// <summary>Facts under which the account leads exactly these гуртки: offices, nothing else.</summary>
    public static LedGroupSourcesRow Leading(IEnumerable<Guid> groupKeys) => new([.. groupKeys], [], null);

    /// <summary>Open memberships that make the person a senior, or none at all.</summary>
    public static IReadOnlyList<MembershipBranchRow> Standing(bool senior) =>
        senior ? [new MembershipBranchRow(MembershipKind.Staff, KurinBranch.USP)] : [];

    /// <summary>
    /// A faked repository under which nobody stands anywhere and nobody leads anything until the test
    /// sets otherwise; a bare mock would hand the rules null.
    /// </summary>
    public static Mock<IResourceScopeRepository> Repository()
    {
        var repository = new Mock<IResourceScopeRepository>();
        repository.Setup(r => r.GetOpenMembershipBranchesAsync(It.IsAny<ResourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Standing(false));
        repository.Setup(r => r.GetLedGroupSourcesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Leading([]));
        return repository;
    }
}
