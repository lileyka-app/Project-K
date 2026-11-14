using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>
/// A card with every required member filled, for tests that care about a few fields only:
/// <c>TestMemberDto.Blank with { FirstName = "…" }</c>. A fresh instance on every read, so no two
/// tests share its collections.
/// </summary>
public static class TestMemberDto
{
    public static MemberDto Blank => new()
    {
        MemberKey = Guid.Empty,
        UserKey = null,
        FirstName = string.Empty,
        MiddleName = null,
        LastName = string.Empty,
        Email = string.Empty,
        PhoneNumber = string.Empty,
        DateOfBirth = null,
        Address = null,
        School = null,
        LatestPlastLevel = null,
        PlastLevelHistories = [],
        LeadershipHistories = [],
        Warnings = [],
        ProfilePhotoUrl = null,
        ProfileVerificationStatus = MemberProfileVerificationStatus.Unverified,
        ProfileVerifiedAtUtc = null,
        ProfileVerifiedByUserKey = null,
        ProfileVerificationNote = null
    };
}
