using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Infrastructure.Tests.KurinModule.RepositoryTests.Integration;

/// <summary>
/// A person is in a kurin because a membership says so, and there is nowhere else left to say it.
/// Fixtures that need someone to be somewhere write one of these.
/// </summary>
internal static class Placing
{
    internal static Membership Of(Member member, Guid kurinKey, Guid? groupKey = null) => new()
    {
        MemberKey = member.MemberKey,
        UserKey = member.UserKey,
        KurinKey = kurinKey,
        GroupKey = groupKey,
        Kind = MembershipKind.Youth,
        JoinedAtUtc = DateTime.UtcNow
    };
}
