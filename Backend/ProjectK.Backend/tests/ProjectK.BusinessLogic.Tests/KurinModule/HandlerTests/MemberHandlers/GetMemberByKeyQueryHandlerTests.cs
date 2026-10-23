using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Members;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberHandlers;

public class GetMemberByKeyHandlerTests
{
    private readonly Mock<IMemberUnitOfWork> _uowMock;
    private readonly Mock<IMemberRepository> _memberRepoMock;
    private readonly Mock<IUnitOfWork> _kurinDataMock = new();
    private readonly Mock<IMembershipRepository> _membershipsMock = new();
    private readonly Mock<IKurinRepository> _kurinsMock = new();
    private readonly Mock<IMentorAssignmentRepository> _mentorRepoMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IResourceAccessService> _accessMock = new();
    private readonly Mock<IAccountProvisioningService> _accountsMock = new();
    private readonly GetMemberByKeyQueryHandler _handler;

    public GetMemberByKeyHandlerTests()
    {
        _memberRepoMock = new Mock<IMemberRepository>();
        _mentorRepoMock = new Mock<IMentorAssignmentRepository>();
        _uowMock = new Mock<IMemberUnitOfWork>();
        _uowMock.Setup(u => u.Members).Returns(_memberRepoMock.Object);

        _mapperMock = new Mock<IMapper>(MockBehavior.Strict);

        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _currentUserContextMock.Setup(c => c.IsInRole(It.IsAny<string>())).Returns(true);

        _kurinDataMock.SetupGet(x => x.Memberships).Returns(_membershipsMock.Object);
        _kurinDataMock.SetupGet(x => x.Kurins).Returns(_kurinsMock.Object);
        _kurinDataMock.SetupGet(x => x.MentorAssignments).Returns(_mentorRepoMock.Object);
        _membershipsMock
            .Setup(x => x.GetActiveForMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _accessMock
            .Setup(a => a.CheckAccessAsync(It.IsAny<ResourceType>(), It.IsAny<ResourceAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("test"));
        _handler = Handler(FieldAccess.SeesEverything());
    }

    [Fact]
    public async Task Handle_WhenMemberExists_ShouldReturnSuccessWithMappedData()
    {
        var memberKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var member = new Member
        {
            MemberKey = memberKey,
            FirstName = "Ivan",
            MiddleName = "I.",
            LastName = "Petrenko",
            Email = "ivan@example.com",
            PhoneNumber = "123456",
            DateOfBirth = new DateOnly(2001, 2, 3)
        };

        _membershipsMock
            .Setup(m => m.GetActiveForMemberAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Membership
            {
                MemberKey = memberKey,
                KurinKey = kurinKey,
                GroupKey = groupKey,
                JoinedAtUtc = DateTime.UtcNow
            }]);

        _memberRepoMock
            .Setup(r => r.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Mock mapper behavior explicitly to avoid invoking real mapping (and its resolver dependencies)
        _mapperMock
            .Setup(m => m.Map<MemberResponse>(member))
            .Returns(new MemberResponse
            {
                MemberKey = member.MemberKey,
                FirstName = member.FirstName,
                MiddleName = member.MiddleName,
                LastName = member.LastName,
                Email = member.Email,
                PhoneNumber = member.PhoneNumber,
                DateOfBirth = member.DateOfBirth,
                ProfilePhotoUrl = null
            });

        var query = new GetMemberByKeyQuery(memberKey);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data!.MemberKey.Should().Be(memberKey);
        result.Data.GroupKey.Should().Be(groupKey);
        result.Data.KurinKey.Should().Be(kurinKey);
        result.Data.FirstName.Should().Be("Ivan");
        result.Data.LastName.Should().Be("Petrenko");

        _memberRepoMock.Verify(r => r.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()), Times.Once);
        _mapperMock.Verify(m => m.Map<MemberResponse>(member), Times.Once);
        _mapperMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_FillsTheGroupsTheyRunAsVykhovnykInTheKurinTheyAreSeenIn()
    {
        var memberKey = Guid.NewGuid();
        var userKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var groupOne = Guid.NewGuid();
        var member = new Member { MemberKey = memberKey, UserKey = userKey, FirstName = "Ivan", MiddleName = "", LastName = "P", Email = "i@e.com", PhoneNumber = "1" };

        _membershipsMock
            .Setup(m => m.GetActiveForMemberAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Membership { MemberKey = memberKey, KurinKey = kurinKey, JoinedAtUtc = DateTime.UtcNow }]);
        _memberRepoMock.Setup(r => r.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _mapperMock.Setup(m => m.Map<MemberResponse>(member)).Returns(new MemberResponse { MemberKey = memberKey });
        _mentorRepoMock
            .Setup(r => r.GetActiveGroupsAsync(userKey, kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GroupRef(groupOne, "Gurtok 1"), new GroupRef(Guid.NewGuid(), "Gurtok 2")]);

        var result = await _handler.Handle(new GetMemberByKeyQuery(memberKey), CancellationToken.None);

        result.Data!.MentoredGroupNames.Should().Equal("Gurtok 1", "Gurtok 2");
        result.Data!.MentoredGroups.First().GroupKey.Should().Be(groupOne);
    }

    [Fact]
    public async Task Handle_WithoutAnAccount_RunsNoGroupAndDoesNotAsk()
    {
        var memberKey = Guid.NewGuid();
        var member = new Member { MemberKey = memberKey, FirstName = "Ivan", MiddleName = "", LastName = "P", Email = "i@e.com", PhoneNumber = "1" };

        _membershipsMock
            .Setup(m => m.GetActiveForMemberAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Membership { MemberKey = memberKey, KurinKey = Guid.NewGuid(), JoinedAtUtc = DateTime.UtcNow }]);
        _memberRepoMock.Setup(r => r.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _mapperMock.Setup(m => m.Map<MemberResponse>(member)).Returns(new MemberResponse { MemberKey = memberKey });

        var result = await _handler.Handle(new GetMemberByKeyQuery(memberKey), CancellationToken.None);

        result.Data!.MentoredGroupNames.Should().BeEmpty();
        _mentorRepoMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenMemberDoesNotExist_ShouldReturnNotFound()
    {
        var memberKey = Guid.NewGuid();
        _memberRepoMock
            .Setup(r => r.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member)null!);

        var query = new GetMemberByKeyQuery(memberKey);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        result.Data.Should().BeNull();

        _memberRepoMock.Verify(r => r.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()), Times.Once);
        _mapperMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ShouldHideWhatThePolicyDoesNotShow_AndSayWhat()
    {
        var (memberKey, groupKey, member) = ArrangeCardOfSomeoneElse();
        var mask = new MemberFieldMask(
            Guid.NewGuid(),
            new Dictionary<MemberDataGroup, CapabilityReach>
            {
                [MemberDataGroup.Contacts] = CapabilityReach.OwnGroup,
                [MemberDataGroup.Birthday] = CapabilityReach.Kurin
            },
            [groupKey]);

        var result = await Handler(FieldAccess.Of(mask)).Handle(new GetMemberByKeyQuery(memberKey), CancellationToken.None);

        var card = result.Data!;
        card.Email.Should().Be(member.Email);
        card.DateOfBirth.Should().BeNull();
        card.Birthday.Should().Be("05-17");
        card.Address.Should().BeNull();
        card.MotherPhone.Should().BeNull();
        card.CardExtras.Should().BeEmpty();
        card.PublicId.Should().BeNull();
        card.HiddenData.Should().Equal("birthDate", "address", "parents", "extras");
    }

    [Fact]
    public async Task Handle_ShouldTellTheAccountStatus_OnlyToWhoeverMayEditThem()
    {
        var (memberKey, _, member) = ArrangeCardOfSomeoneElse();
        var account = member.UserKey!.Value;
        _accountsMock
            .Setup(a => a.FindAsync(account, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountSnapshot(account, member.Email, OnboardingStatus.PendingActivation));

        var denied = await _handler.Handle(new GetMemberByKeyQuery(memberKey), CancellationToken.None);
        _accessMock
            .Setup(a => a.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());
        var allowed = await _handler.Handle(new GetMemberByKeyQuery(memberKey), CancellationToken.None);

        denied.Data!.AccountStatus.Should().BeNull();
        allowed.Data!.AccountStatus.Should().Be(OnboardingStatus.PendingActivation);
    }

    private (Guid MemberKey, Guid GroupKey, Member Member) ArrangeCardOfSomeoneElse()
    {
        var memberKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var member = new Member
        {
            MemberKey = memberKey,
            UserKey = Guid.NewGuid(),
            FirstName = "Ivan",
            MiddleName = "",
            LastName = "P",
            Email = "i@e.com",
            PhoneNumber = "+380501234567",
            DateOfBirth = new DateOnly(2010, 5, 17)
        };

        _membershipsMock
            .Setup(m => m.GetActiveForMemberAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Membership { MemberKey = memberKey, KurinKey = Guid.NewGuid(), GroupKey = groupKey, JoinedAtUtc = DateTime.UtcNow }]);
        _memberRepoMock.Setup(r => r.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _mentorRepoMock
            .Setup(r => r.GetActiveGroupsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mapperMock.Setup(m => m.Map<MemberResponse>(member)).Returns(() => new MemberResponse
        {
            MemberKey = memberKey,
            UserKey = member.UserKey,
            PublicId = "PL-1",
            FirstName = member.FirstName,
            MiddleName = "",
            LastName = member.LastName,
            Email = member.Email,
            PhoneNumber = member.PhoneNumber,
            DateOfBirth = member.DateOfBirth,
            Address = "Street 1",
            MotherPhone = "+380501112233",
            CardExtras = new Dictionary<string, string> { ["visa"] = "D" }
        });
        return (memberKey, groupKey, member);
    }

    private GetMemberByKeyQueryHandler Handler(IMemberFieldAccess fieldAccess) => new(
        _uowMock.Object,
        _mapperMock.Object,
        _currentUserContextMock.Object,
        fieldAccess,
        _accessMock.Object,
        _kurinDataMock.Object,
        _accountsMock.Object);
}
