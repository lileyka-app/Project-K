using AutoMapper;
using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;
using MemberEntity = ProjectK.Common.Entities.KurinModule.Member;
using MembershipEntity = ProjectK.Common.Entities.KurinModule.Membership;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberHandlers;

/// <summary>
/// The public code is the one thing a person gives away themselves. A провід that could read it
/// could sign them into another kurin without ever asking, so nobody but its owner is told it.
/// </summary>
public class PublicIdIsOnlyForItsOwnerTests
{
    private const string Code = "PL-4F7K2-9M4A1";

    private readonly Mock<IMemberUnitOfWork> _memberData = new();
    private readonly Mock<IMemberRepository> _members = new();
    private readonly Mock<IUnitOfWork> _kurinData = new();
    private readonly Mock<IMembershipRepository> _memberships = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IMapper> _mapper = new();

    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _ownerUserKey = Guid.NewGuid();

    public PublicIdIsOnlyForItsOwnerTests()
    {
        _mapper
            .Setup(m => m.Map<MemberResponse>(It.IsAny<object>()))
            .Returns(() => new MemberResponse
            {
                MemberKey = _memberKey,
                PublicId = Code,
                UserKey = _ownerUserKey,
                FirstName = "Оксана",
                MiddleName = string.Empty,
                LastName = "Тестова",
                Email = "o@example.com",
                PhoneNumber = "0000000000"
            });

        _memberData.SetupGet(u => u.Members).Returns(_members.Object);
        _kurinData.SetupGet(u => u.Memberships).Returns(_memberships.Object);
        var mentorAssignments = new Mock<IMentorAssignmentRepository>();
        mentorAssignments
            .Setup(r => r.GetActiveGroupsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _kurinData.SetupGet(u => u.MentorAssignments).Returns(mentorAssignments.Object);

        _members
            .Setup(r => r.GetByKeyAsync(_memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberEntity
            {
                MemberKey = _memberKey,
                PublicId = Code,
                UserKey = _ownerUserKey,
                FirstName = "Оксана",
                LastName = "Тестова",
                Email = "o@example.com",
                PhoneNumber = "0000000000"
            });
        _memberships
            .Setup(r => r.GetActiveForMemberAsync(_memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MembershipEntity { MemberKey = _memberKey, KurinKey = _kurinKey }]);

        _currentUser.SetupGet(c => c.KurinKey).Returns(_kurinKey);
    }

    [Fact]
    public async Task TheirOwnCode_ShouldBeTheirsToRead()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(_ownerUserKey);

        var result = await Handler().Handle(new GetMemberByKeyQuery(_memberKey), CancellationToken.None);

        result.Data!.PublicId.Should().Be(Code);
    }

    [Fact]
    public async Task SomeoneElsesCode_ShouldNotComeBack_EvenToTheKurinsLeadership()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        _currentUser.SetupGet(c => c.Roles).Returns(["KV.Zvyazkovyi"]);

        var result = await Handler().Handle(new GetMemberByKeyQuery(_memberKey), CancellationToken.None);

        result.Data!.PublicId.Should().BeNull();
    }

    private GetMemberByKeyQueryHandler Handler()
    {
        var access = new Mock<IResourceAccessService>();
        access
            .Setup(a => a.CheckAccessAsync(It.IsAny<ResourceType>(), It.IsAny<ResourceAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());
        return new(
            _memberData.Object,
            _mapper.Object,
            _currentUser.Object,
            FieldAccess.SeesEverything(_currentUser.Object.UserId),
            access.Object,
            _kurinData.Object,
            new Mock<ProjectK.Common.Interfaces.Modules.AuthModule.IAccountProvisioningService>().Object);
    }
}
