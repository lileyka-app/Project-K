using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.KurinScope;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.KurinScope;

/// <summary>
/// What the toolbar is allowed to offer. The list has to be the kurins the server would actually let
/// this account stand in — offering one it would refuse is worse than offering nothing.
/// </summary>
public class GetKurinScopeOptionsHandlerTests
{
    private readonly Mock<IMembershipDirectory> _memberships = new();
    private readonly GetKurinScopeOptionsQueryHandler _handler;
    private readonly Guid _userKey = Guid.NewGuid();

    public GetKurinScopeOptionsHandlerTests()
    {
        _handler = new GetKurinScopeOptionsQueryHandler(_memberships.Object);
    }

    [Fact]
    public async Task EveryCurrentMembership_ShouldBeOffered_ByKurinNumber()
    {
        Current(Membership(42, KurinBranch.USP, MembershipKind.Staff), Membership(7, KurinBranch.UPYu));

        var result = await _handler.Handle(new GetKurinScopeOptionsQuery(_userKey), CancellationToken.None);

        result.Data!.Select(o => o.KurinNumber).Should().Equal(7, 42);
        result.Data.Last().Branch.Should().Be(KurinBranch.USP);
        result.Data.Last().Kind.Should().Be(MembershipKind.Staff);
    }

    [Fact]
    public async Task BelongingNowhere_ShouldBeAnEmptyList_NotAFailure()
    {
        Current();

        var result = await _handler.Handle(new GetKurinScopeOptionsQuery(_userKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().BeEmpty();
    }

    private void Current(params MembershipRecord[] memberships)
        => _memberships
            .Setup(d => d.GetCurrentForAccountAsync(_userKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberships);

    private static MembershipRecord Membership(
        int number,
        KurinBranch branch,
        MembershipKind kind = MembershipKind.Youth)
        => new(
            Guid.NewGuid(), Guid.NewGuid(), number, branch, null, null, null, kind,
            DateTime.UtcNow.AddYears(-1), null);
}
