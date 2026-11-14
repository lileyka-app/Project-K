using FluentAssertions;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Leadership;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.BusinessLogic.Tests.MemberModule;

/// <summary>A провід as the client reads it: the entity it leads, and each holder of its roles.</summary>
public class LeadershipMappingTests
{
    [Theory]
    [InlineData(LeadershipType.Kurin)]
    [InlineData(LeadershipType.KV)]
    [InlineData(LeadershipType.Group)]
    public void ToDto_ShouldNameTheEntityItLeads(LeadershipType type)
    {
        var kurinKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var leadership = new Leadership
        {
            LeadershipKey = Guid.NewGuid(),
            Type = type,
            KurinKey = type == LeadershipType.Group ? null : kurinKey,
            GroupKey = type == LeadershipType.Group ? groupKey : null,
            StartDate = new DateOnly(2025, 9, 1)
        };

        var dto = leadership.ToDto(FakeBlobReadLinks.Instance);

        dto.EntityKey.Should().Be(type == LeadershipType.Group ? groupKey : kurinKey);
        dto.LeadershipKey.Should().Be(leadership.LeadershipKey);
        dto.StartDate.Should().Be(leadership.StartDate);
        dto.LeadershipHistories.Should().BeEmpty();
    }

    [Fact]
    public void ToHolderDto_ShouldNameTheRole_AndThePersonWhenTheyWereLoaded()
    {
        var holder = new Member
        {
            MemberKey = Guid.NewGuid(),
            UserKey = Guid.NewGuid(),
            FirstName = "Ірина",
            MiddleName = "Петрівна",
            LastName = "Нова",
            ProfilePhotoBlobName = "h.jpg"
        };
        var seated = new LeadershipHistory
        {
            LeadershipHistoryKey = Guid.NewGuid(),
            LeadershipKey = Guid.NewGuid(),
            MemberKey = holder.MemberKey,
            Member = holder,
            Role = LeadershipRole.Pysar,
            StartDate = new DateOnly(2025, 9, 1)
        };
        var unloaded = new LeadershipHistory { MemberKey = Guid.NewGuid(), Role = LeadershipRole.Kurinnuy };

        var dto = seated.ToHolderDto(FakeBlobReadLinks.Instance);

        dto.LeadershipHistoryKey.Should().Be(seated.LeadershipHistoryKey);
        dto.LeadershipKey.Should().BeNull("the провід's key is not repeated on each holder");
        dto.Role.Should().Be(nameof(LeadershipRole.Pysar));
        dto.Member.MemberKey.Should().Be(holder.MemberKey);
        dto.Member.ProfilePhotoUrl.Should().Be(FakeBlobReadLinks.Instance.For("h.jpg"));
        unloaded.ToHolderDto(FakeBlobReadLinks.Instance).Member.Should().BeNull();
    }
}
