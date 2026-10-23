using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Join;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Lookup;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MembershipHandlers;

/// <summary>
/// Taking someone into a second kurin. The провід has a code the person gave them, not a key from
/// this kurin's database — everything here is about that being enough, and about it saying no more
/// than it has to.
/// </summary>
public class JoinKurinByPublicIdTests
{
    private const string Code = "PL-4F7K2-9M4A1";

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly Mock<IMembershipRepository> _memberships = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IAccessChangeSignal> _accessChanges = new();

    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly Guid _kurinKey = Guid.NewGuid();

    private readonly JoinKurinCommandHandler _join;
    private readonly FindMemberByPublicIdQueryHandler _lookup;

    public JoinKurinByPublicIdTests()
    {
        _unitOfWork.SetupGet(u => u.Memberships).Returns(_memberships.Object);
        _unitOfWork.SetupGet(u => u.Kurins).Returns(_kurins.Object);
        _unitOfWork.SetupGet(u => u.Groups).Returns(_groups.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _kurins
            .Setup(r => r.GetByKeyAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Kurin(42) { KurinKey = _kurinKey });

        _members
            .Setup(d => d.FindByPublicIdAsync(Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberCard(_memberKey, Code, "Оксана", "Тестова", null, 1));
        _members
            .Setup(d => d.FindAsync(_memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(
                _memberKey, Guid.NewGuid(), Guid.NewGuid(), null, "Оксана", "Тестова", "o@example.com", null));

        _join = new JoinKurinCommandHandler(_unitOfWork.Object, _members.Object, _accessChanges.Object);
        _lookup = new FindMemberByPublicIdQueryHandler(_members.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task ACode_ShouldBeEnoughToTakeSomeoneIn()
    {
        Membership? opened = null;
        _memberships.Setup(r => r.Open(It.IsAny<Membership>()))
            .Callback<Membership>(membership => opened = membership);

        var result = await _join.Handle(
            new JoinKurinCommand(Guid.Empty, _kurinKey, null, MembershipKind.Youth, Code),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Created);
        opened.Should().NotBeNull();
        opened!.MemberKey.Should().Be(_memberKey);
        opened.KurinKey.Should().Be(_kurinKey);
        _accessChanges.Verify(s => s.MarkUser(opened.UserKey!.Value), Times.Once);
    }

    [Fact]
    public async Task ACodeNobodyHas_ShouldBeRefused_WithoutOpeningAnything()
    {
        var result = await _join.Handle(
            new JoinKurinCommand(Guid.Empty, _kurinKey, null, MembershipKind.Youth, "PL-00000-00000"),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        result.ErrorCode.Should().Be("NoSuchCode");
        _memberships.Verify(r => r.Open(It.IsAny<Membership>()), Times.Never);
    }

    [Fact]
    public async Task NeitherAKeyNorACode_ShouldBeRefused()
    {
        var result = await _join.Handle(
            new JoinKurinCommand(Guid.Empty, _kurinKey, null), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("MembershipKeysRequired");
    }

    [Fact]
    public async Task SomeoneAlreadyHere_ShouldBeRefusedAsAConflict()
    {
        _memberships
            .Setup(r => r.GetActiveAsync(_memberKey, _kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Membership { MemberKey = _memberKey, KurinKey = _kurinKey });

        var result = await _join.Handle(
            new JoinKurinCommand(Guid.Empty, _kurinKey, null, MembershipKind.Youth, Code),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        _memberships.Verify(r => r.Open(It.IsAny<Membership>()), Times.Never);
    }

    [Fact]
    public async Task TheCard_ShouldSayHowManyKurinsButNotWhich()
    {
        var result = await _lookup.Handle(
            new FindMemberByPublicIdQuery(_kurinKey, Code), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.Member.FullName.Should().Be("Оксана Тестова");
        result.Data.Member.CurrentMembershipCount.Should().Be(1);
        result.Data.AlreadyInThisKurin.Should().BeFalse();

        // The card is a record with a fixed shape; this pins that nothing kurin-shaped crept into it.
        typeof(MemberCard).GetProperties().Select(p => p.Name)
            .Should().NotContain(name => name.Contains("Kurin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TheCard_ShouldSayWhenTheyAreAlreadyHere()
    {
        _memberships
            .Setup(r => r.GetActiveAsync(_memberKey, _kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Membership { MemberKey = _memberKey, KurinKey = _kurinKey });

        var result = await _lookup.Handle(
            new FindMemberByPublicIdQuery(_kurinKey, Code), CancellationToken.None);

        result.Data!.AlreadyInThisKurin.Should().BeTrue();
    }

    [Fact]
    public async Task AnUnknownCode_ShouldAnswerTheSameWayAsAMalformedOne()
    {
        var unknown = await _lookup.Handle(
            new FindMemberByPublicIdQuery(_kurinKey, "PL-00000-00000"), CancellationToken.None);
        var malformed = await _lookup.Handle(
            new FindMemberByPublicIdQuery(_kurinKey, "not-a-code"), CancellationToken.None);

        unknown.Type.Should().Be(ResultType.NotFound);
        malformed.Type.Should().Be(ResultType.NotFound);
        unknown.ErrorCode.Should().Be(malformed.ErrorCode);
    }
}
