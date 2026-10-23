using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.ProfileVerification;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using Xunit;
using KurinEntity = ProjectK.Common.Entities.KurinModule.Kurin;
using MentorAssignmentEntity = ProjectK.Common.Entities.KurinModule.MentorAssignment;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberHandlers;

public class MemberProfileVerificationHandlerTests
{
    private readonly Mock<IMemberUnitOfWork> _uowMock;
    private readonly Mock<IMemberRepository> _memberRepoMock;
    private readonly Mock<IUnitOfWork> _kurinDataMock = new();
    private readonly Mock<IMembershipRepository> _membershipsMock = new();
    private readonly Mock<IKurinRepository> _kurinsMock = new();
    private readonly Mock<IMentorAssignmentRepository> _mentorAssignmentRepoMock;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IDomainEventPublisher> _eventsMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IResourceScopeReader> _scopeReaderMock;
    private readonly MemberProfileVerificationService _service;

    public MemberProfileVerificationHandlerTests()
    {
        _memberRepoMock = new Mock<IMemberRepository>();
        _mentorAssignmentRepoMock = new Mock<IMentorAssignmentRepository>();
        _uowMock = new Mock<IMemberUnitOfWork>();
        _uowMock.SetupGet(x => x.Members).Returns(_memberRepoMock.Object);

        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _eventsMock = new Mock<IDomainEventPublisher>();
        _mapperMock = new Mock<IMapper>();
        _mapperMock
            .Setup(x => x.Map<MemberResponse>(It.IsAny<object>()))
            .Returns(() => new MemberResponse());
        _scopeReaderMock = new Mock<IResourceScopeReader>();
        _scopeReaderMock
            .Setup(x => x.GetLedGroupKeysAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        _kurinDataMock.SetupGet(x => x.Memberships).Returns(_membershipsMock.Object);
        _kurinDataMock.SetupGet(x => x.Kurins).Returns(_kurinsMock.Object);
        _membershipsMock
            .Setup(x => x.GetActiveForMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _service = new MemberProfileVerificationService(
            _uowMock.Object,
            _currentUserContextMock.Object,
            _eventsMock.Object,
            _mapperMock.Object,
            _scopeReaderMock.Object,
            _kurinDataMock.Object,
            FieldAccess.SeesEverything());
    }

    [Fact]
    public async Task Verify_AsManagerInSameKurin_ShouldMarkVerifiedCurrent()
    {
        var actorUserKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var member = CreateMember(kurinKey);

        SetupCurrentUser(actorUserKey, kurinKey, "KV.Zvyazkovyi");
        SetupMember(member);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new VerifyMemberProfileCommandHandler(_service);
        var result = await handler.Handle(
            new VerifyMemberProfileCommand(member.MemberKey, " checked "),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        member.ProfileVerificationStatus.Should().Be(MemberProfileVerificationStatus.VerifiedCurrent);
        member.ProfileVerifiedAtUtc.Should().NotBeNull();
        member.ProfileVerifiedByUserKey.Should().Be(actorUserKey);
        member.ProfileVerificationNote.Should().Be("checked");
        result.Data!.ProfileVerificationStatus.Should().Be(MemberProfileVerificationStatus.VerifiedCurrent);

        _memberRepoMock.Verify(x => x.Update(member, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verify_LinkedMember_ShouldNotifyMemberOwner()
    {
        var actorUserKey = Guid.NewGuid();
        var memberUserKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var member = CreateMember(kurinKey);
        member.UserKey = memberUserKey;

        SetupCurrentUser(actorUserKey, kurinKey, "KV.Zvyazkovyi");
        SetupMember(member);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.VerifyAsync(member.MemberKey, null, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _eventsMock.Verify(x => x.PublishAsync(
            It.Is<MemberProfileVerified>(raised =>
                raised.MemberKey == member.MemberKey
                && raised.MemberUserKey == memberUserKey
                && raised.ActorUserKey == actorUserKey),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Verify_WhenKurinToggleDisabled_ShouldReturnBadRequest()
    {
        var kurinKey = Guid.NewGuid();
        var member = CreateMember(kurinKey, profileVerificationEnabled: false);
        SetupCurrentUser(Guid.NewGuid(), kurinKey, "KV.Zvyazkovyi");
        _memberRepoMock
            .Setup(x => x.GetByKeyAsync(member.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var result = await _service.VerifyAsync(member.MemberKey, null, CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("ProfileVerificationDisabled");
        _memberRepoMock.Verify(x => x.Update(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Verify_WhenActorIsSameUserAsMember_ShouldReturnForbidden()
    {
        var userKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var member = CreateMember(kurinKey);
        member.UserKey = userKey;

        SetupCurrentUser(userKey, kurinKey, "KV.Zvyazkovyi");
        _memberRepoMock
            .Setup(x => x.GetByKeyAsync(member.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var result = await _service.VerifyAsync(member.MemberKey, null, CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        _memberRepoMock.Verify(x => x.Update(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reset_AsManagerInSameKurin_ShouldClearVerificationFields()
    {
        var actorUserKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var member = CreateMember(kurinKey);
        member.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedCurrent;
        member.ProfileVerifiedAtUtc = DateTime.UtcNow.AddDays(-1);
        member.ProfileVerifiedByUserKey = Guid.NewGuid();
        member.ProfileVerificationNote = "existing";

        SetupCurrentUser(actorUserKey, kurinKey, "KV.Zvyazkovyi");
        SetupMember(member);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new ResetMemberProfileVerificationCommandHandler(_service);
        var result = await handler.Handle(
            new ResetMemberProfileVerificationCommand(member.MemberKey),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        member.ProfileVerificationStatus.Should().Be(MemberProfileVerificationStatus.Unverified);
        member.ProfileVerifiedAtUtc.Should().BeNull();
        member.ProfileVerifiedByUserKey.Should().BeNull();
        member.ProfileVerificationNote.Should().BeNull();
    }

    [Fact]
    public async Task Verify_AsMentorAssignedToMembersGroup_ShouldSucceed()
    {
        var mentorUserKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var member = CreateMember(kurinKey, groupKey: groupKey);

        SetupCurrentUser(mentorUserKey, kurinKey, "KV.Vykhovnyk");
        SetupMember(member);
        _scopeReaderMock.Setup(x => x.GetLedGroupKeysAsync(mentorUserKey, kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { groupKey });
        _mentorAssignmentRepoMock
            .Setup(x => x.GetByMentorUserKeyAsync(mentorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MentorAssignmentEntity>
            {
                new() { MentorUserKey = mentorUserKey, GroupKey = groupKey, AssignedAtUtc = DateTime.UtcNow.AddDays(-1) }
            });
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.VerifyAsync(member.MemberKey, null, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        member.ProfileVerificationStatus.Should().Be(MemberProfileVerificationStatus.VerifiedCurrent);
    }

    [Fact]
    public async Task Verify_AsMentorWithoutActiveAssignment_ShouldReturnForbidden()
    {
        var mentorUserKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var member = CreateMember(kurinKey, groupKey: Guid.NewGuid());

        SetupCurrentUser(mentorUserKey, kurinKey, "KV.Vykhovnyk");
        _memberRepoMock
            .Setup(x => x.GetByKeyAsync(member.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _mentorAssignmentRepoMock
            .Setup(x => x.GetByMentorUserKeyAsync(mentorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MentorAssignmentEntity>());

        var result = await _service.VerifyAsync(member.MemberKey, null, CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        _memberRepoMock.Verify(x => x.Update(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupCurrentUser(Guid userKey, Guid kurinKey, string role)
    {
        _currentUserContextMock.SetupGet(x => x.UserId).Returns(userKey);
        _currentUserContextMock.SetupGet(x => x.KurinKey).Returns(kurinKey);
        _currentUserContextMock.SetupGet(x => x.Roles).Returns(new[] { role });
        _currentUserContextMock
            .Setup(x => x.IsInRole(It.IsAny<string>()))
            .Returns<string>(requestedRole => string.Equals(requestedRole, role, StringComparison.OrdinalIgnoreCase));
    }

    private void SetupMember(Member member)
    {
        _memberRepoMock
            .Setup(x => x.GetByKeyAsync(member.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _mapperMock
            .Setup(x => x.Map<MemberResponse>(member))
            .Returns(() => MapMember(member));
    }

    /// <summary>
    /// Builds the person and, because that is now the only thing that places them, the membership
    /// and the kurin the caller will read through.
    /// </summary>
    private Member CreateMember(
        Guid kurinKey,
        bool profileVerificationEnabled = true,
        Guid? groupKey = null)
    {
        var member = new Member
        {
            MemberKey = Guid.NewGuid(),
            FirstName = "Ivan",
            MiddleName = "I.",
            LastName = "Petrenko",
            Email = "ivan@example.com",
            PhoneNumber = "123",
            DateOfBirth = new DateOnly(2000, 1, 1)
        };

        _membershipsMock
            .Setup(x => x.GetActiveForMemberAsync(member.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Membership
            {
                MemberKey = member.MemberKey,
                KurinKey = kurinKey,
                GroupKey = groupKey,
                JoinedAtUtc = DateTime.UtcNow.AddYears(-1)
            }]);
        _kurinsMock
            .Setup(x => x.GetByKeyAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KurinEntity(1)
            {
                KurinKey = kurinKey,
                ProfileVerificationEnabled = profileVerificationEnabled
            });

        return member;
    }

    private static MemberResponse MapMember(Member member)
    {
        return new MemberResponse
        {
            MemberKey = member.MemberKey,
            FirstName = member.FirstName,
            MiddleName = member.MiddleName,
            LastName = member.LastName,
            Email = member.Email,
            PhoneNumber = member.PhoneNumber,
            DateOfBirth = member.DateOfBirth,
            ProfileVerificationStatus = member.ProfileVerificationStatus,
            ProfileVerifiedAtUtc = member.ProfileVerifiedAtUtc,
            ProfileVerifiedByUserKey = member.ProfileVerifiedByUserKey,
            ProfileVerificationNote = member.ProfileVerificationNote
        };
    }
}
