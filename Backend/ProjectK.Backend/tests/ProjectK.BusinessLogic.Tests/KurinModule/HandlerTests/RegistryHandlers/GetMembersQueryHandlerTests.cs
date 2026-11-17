using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Registry;
using ProjectK.BusinessLogic.Tests.MemberModule.HandlerTests.MemberHandlers;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.RegistryHandlers;

public class GetMembersHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IMembershipRepository> _memberRepoMock;
    private readonly Mock<IMentorAssignmentRepository> _mentorRepoMock;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly GetMembersQueryHandler _handler;

    public GetMembersHandlerTests()
    {
        _memberRepoMock = new Mock<IMembershipRepository>();
        _mentorRepoMock = new Mock<IMentorAssignmentRepository>();
        _uowMock = new Mock<IUnitOfWork>();
        _uowMock.Setup(u => u.Memberships).Returns(_memberRepoMock.Object);

        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _currentUserContextMock.Setup(c => c.IsInRole(It.IsAny<string>())).Returns(true); // Allow all by default for tests

        _handler = new GetMembersQueryHandler(_uowMock.Object, FakeBlobReadLinks.Instance, FieldAccess.SeesEverything(), Mock.Of<IKurinTagVisibility>());
    }

    private static MemberListRow MakeItem(Guid groupKey, Guid kurinKey, string first, string last,
        string? blob = null, Guid? userKey = null, List<MemberListOffice>? offices = null)
    {
        var memberKey = Guid.NewGuid();
        return new()
        {
            MemberKey = memberKey,
            GroupKey = groupKey,
            GroupName = null,
            KurinKey = kurinKey,
            UserKey = userKey,
            FirstName = first,
            MiddleName = "M",
            LastName = last,
            Email = $"{first.ToLower()}@example.com",
            PhoneNumber = "123456",
            DateOfBirth = new DateOnly(2000, 1, 1),
            Address = null,
            School = null,
            StoredPlastLevel = null,
            ProfilePhotoBlobName = blob,
            ProfileVerificationStatus = default,
            ProfileVerifiedAtUtc = null,
            ProfileVerifiedByUserKey = null,
            ProfileVerificationNote = null,
            PlastLevels = [],
            Offices = offices ?? [],
            Assignments = [],
            Warnings = []
        };
    }

    [Fact]
    public async Task Handle_GroupKeyOnly_ShouldReturnMembersFromGroup()
    {
        var groupKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var members = new List<MemberListRow>
        {
            MakeItem(groupKey, kurinKey, "A","One","a.png"),
            MakeItem(groupKey, kurinKey, "B","Two", null)
        };

        _memberRepoMock
            .Setup(r => r.GetRosterRowsByGroupAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        var query = new GetMembersQuery(groupKey, Guid.Empty);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().HaveCount(2);
        var list = result.Data!.ToList();
        list[0].FirstName.Should().Be("A");
        list[0].ProfilePhotoUrl.Should().Be("https://cdn.test/a.png?sig=read");
        list[1].FirstName.Should().Be("B");
        list[1].ProfilePhotoUrl.Should().BeNull();

        _memberRepoMock.Verify(r => r.GetRosterRowsByGroupAsync(groupKey, It.IsAny<CancellationToken>()), Times.Once);
        _memberRepoMock.Verify(r => r.GetRosterRowsByKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_KurinKeyOnly_ShouldReturnMembersFromKurin()
    {
        var kurinKey = Guid.NewGuid();
        var members = new List<MemberListRow>
        {
            MakeItem(Guid.NewGuid(), kurinKey, "C", "Three"),
            MakeItem(Guid.NewGuid(), kurinKey, "D", "Four")
        };

        _memberRepoMock
            .Setup(r => r.GetRosterRowsByKurinAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        var query = new GetMembersQuery(Guid.Empty, kurinKey);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().HaveCount(2);
        result.Data!.Select(x => x.FirstName).Should().BeEquivalentTo(new[] { "C", "D" });

        _memberRepoMock.Verify(r => r.GetRosterRowsByKurinAsync(kurinKey, It.IsAny<CancellationToken>()), Times.Once);
        _memberRepoMock.Verify(r => r.GetRosterRowsByGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldIncludeUserRole_WhenMemberHasLinkedUser()
    {
        var kurinKey = Guid.NewGuid();
        var userKey = Guid.NewGuid();
        var office = new MemberListOffice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), LeadershipRole.Hurtkoviy, LeadershipType.Group,
            null, "Alpha", new DateOnly(2024, 1, 1), null, null);
        var member = MakeItem(Guid.NewGuid(), kurinKey, "Lead", "Mentor", userKey: userKey, offices: [office]);

        _memberRepoMock
            .Setup(r => r.GetRosterRowsByKurinAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemberListRow> { member });

        var result = await _handler.Handle(new GetMembersQuery(Guid.Empty, kurinKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.Single().UserRole.Should().Be("Group.Hurtkoviy");
    }

    [Fact]
    public async Task Handle_BothKeysProvided_ShouldReturnBadRequest()
    {
        var query = new GetMembersQuery(Guid.NewGuid(), Guid.NewGuid());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.Data.Should().BeNull();

        _memberRepoMock.Verify(r => r.GetRosterRowsByGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _memberRepoMock.Verify(r => r.GetRosterRowsByKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GroupKeyOnly_NoMembers_ShouldReturnEmptySuccess()
    {
        var groupKey = Guid.NewGuid();
        _memberRepoMock
            .Setup(r => r.GetRosterRowsByGroupAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemberListRow>());

        var result = await _handler.Handle(new GetMembersQuery(groupKey, Guid.Empty), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        var groupKey = Guid.NewGuid();
        var expected = new Exception("DB error");
        _memberRepoMock
            .Setup(r => r.GetRosterRowsByGroupAsync(groupKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var query = new GetMembersQuery(groupKey, Guid.Empty);

        var ex = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(query, CancellationToken.None));
        ex.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Handle_MappingConsistency_ShouldMatchDirectMapping()
    {
        var groupKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var members = new List<MemberListRow>
        {
            MakeItem(groupKey, kurinKey, "X","One","pic1.jpg"),
            MakeItem(groupKey, kurinKey, "Y","Two","pic 2.png")
        };

        _memberRepoMock
            .Setup(r => r.GetRosterRowsByGroupAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        var result = await _handler.Handle(new GetMembersQuery(groupKey, Guid.Empty), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        // A viewer who sees everything still gets the birthday filled in by the mask.
        var direct = members.Select(member => member.ToDto(FakeBlobReadLinks.Instance) with { Birthday = "01-01" }).ToList();
        result.Data.Should().BeEquivalentTo(direct);
    }

    [Fact]
    public async Task Handle_ShouldMaskEachRow_ByTheGroupItStandsIn()
    {
        var viewer = Guid.NewGuid();
        var ownGroup = Guid.NewGuid();
        var otherGroup = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        MemberListRow Lived(MemberListRow row) => row with { Address = "Street 1", DateOfBirth = new DateOnly(2011, 3, 9) };
        var mate = Lived(MakeItem(ownGroup, kurinKey, "Mate", "Own"));
        var stranger = Lived(MakeItem(otherGroup, kurinKey, "Stranger", "Other"));
        var self = Lived(MakeItem(otherGroup, kurinKey, "Self", "Me", userKey: viewer));

        _memberRepoMock
            .Setup(r => r.GetRosterRowsByKurinAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mate, stranger, self]);
        var mask = new MemberFieldMask(
            viewer,
            new Dictionary<MemberDataGroup, CapabilityReach>
            {
                [MemberDataGroup.Contacts] = CapabilityReach.OwnGroup,
                [MemberDataGroup.Birthday] = CapabilityReach.Kurin
            },
            [ownGroup]);
        var handler = new GetMembersQueryHandler(_uowMock.Object, FakeBlobReadLinks.Instance, FieldAccess.Of(mask), Mock.Of<IKurinTagVisibility>());

        var result = (await handler.Handle(new GetMembersQuery(Guid.Empty, kurinKey), CancellationToken.None)).Data!.ToList();

        var mateRow = result.Single(m => m.FirstName == "Mate");
        mateRow.Email.Should().Be("mate@example.com");
        mateRow.DateOfBirth.Should().BeNull();
        mateRow.Birthday.Should().Be("03-09");
        mateRow.Address.Should().BeNull();
        mateRow.HiddenData.Should().Equal("birthDate", "address", "parents", "extras");

        var strangerRow = result.Single(m => m.FirstName == "Stranger");
        strangerRow.Email.Should().BeEmpty();
        strangerRow.PhoneNumber.Should().BeEmpty();
        strangerRow.HiddenData.Should().Equal("contacts", "birthDate", "address", "parents", "extras");

        var selfRow = result.Single(m => m.FirstName == "Self");
        selfRow.Email.Should().Be("self@example.com");
        selfRow.DateOfBirth.Should().Be(new DateOnly(2011, 3, 9));
        selfRow.Address.Should().Be("Street 1");
        selfRow.HiddenData.Should().BeEmpty();
    }
}
