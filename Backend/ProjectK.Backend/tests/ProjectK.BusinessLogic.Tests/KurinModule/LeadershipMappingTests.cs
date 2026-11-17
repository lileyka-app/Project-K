using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule;

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

        var dto = leadership.ToDto(new Dictionary<Guid, MemberLookupDto>());

        dto.EntityKey.Should().Be(type == LeadershipType.Group ? groupKey : kurinKey);
        dto.LeadershipKey.Should().Be(leadership.LeadershipKey);
        dto.StartDate.Should().Be(leadership.StartDate);
        dto.LeadershipHistories.Should().BeEmpty();
    }

    [Fact]
    public void ToHolderDto_ShouldNameTheRole_AndThePersonWhenTheyWereLookedUp()
    {
        var holder = new MemberLookupDto
        {
            MemberKey = Guid.NewGuid(),
            UserKey = Guid.NewGuid(),
            FirstName = "Ірина",
            MiddleName = "Петрівна",
            LastName = "Нова",
            ProfilePhotoBlobName = "h.jpg",
            ProfilePhotoUrl = FakeBlobReadLinks.Instance.For("h.jpg")
        };
        var people = new Dictionary<Guid, MemberLookupDto> { [holder.MemberKey] = holder };
        var seated = new LeadershipHistory
        {
            LeadershipHistoryKey = Guid.NewGuid(),
            LeadershipKey = Guid.NewGuid(),
            MemberKey = holder.MemberKey,
            Role = LeadershipRole.Pysar,
            StartDate = new DateOnly(2025, 9, 1)
        };
        var unloaded = new LeadershipHistory { MemberKey = Guid.NewGuid(), Role = LeadershipRole.Kurinnuy };

        var dto = seated.ToHolderDto(people);

        dto.LeadershipHistoryKey.Should().Be(seated.LeadershipHistoryKey);
        dto.LeadershipKey.Should().BeNull("the провід's key is not repeated on each holder");
        dto.Role.Should().Be(nameof(LeadershipRole.Pysar));
        dto.Member.MemberKey.Should().Be(holder.MemberKey);
        dto.Member.ProfilePhotoUrl.Should().Be(FakeBlobReadLinks.Instance.For("h.jpg"));
        unloaded.ToHolderDto(people).Member.Should().BeNull();
    }
}
