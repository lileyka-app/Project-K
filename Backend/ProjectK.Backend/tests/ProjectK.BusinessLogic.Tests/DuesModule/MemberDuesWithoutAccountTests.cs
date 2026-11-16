using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Member;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

/// <summary>
/// Someone the kurin never charged still sees a вкладка, and its zero is in the kurin's own
/// money — a Czech kurin shows koruny, not hryvni.
/// </summary>
public class MemberDuesWithoutAccountTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Person = Guid.NewGuid();

    private readonly Mock<IMembershipDirectory> _memberships = new();

    private GetMemberDuesQueryHandler Handler()
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.KurinKey).Returns(Kurin);
        var dues = new Mock<IDuesUnitOfWork> { DefaultValue = DefaultValue.Mock };
        var unitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };

        return new GetMemberDuesQueryHandler(
            dues.Object,
            new DuesLedgerReader(dues.Object, Mock.Of<IDuesAccrual>()),
            unitOfWork.Object,
            _memberships.Object,
            Mock.Of<IMemberDirectory>(),
            Mock.Of<IResourceAccessService>(),
            user.Object,
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero)),
            KraiProfiles.Paying(Kurin, KraiDuesNames.Stanytsia));
    }

    [Fact]
    public async Task SomeoneWhoNeverBelongedHere_ShouldSeeTheKraisCurrency()
    {
        _memberships.Setup(m => m.GetForMemberAsync(Person, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await Handler().Handle(new GetMemberDuesQuery(Person), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.HasAccount.Should().BeFalse();
        result.Data.Currency.Should().Be("CZK");
        result.Data.NumberLocale.Should().Be("cs-CZ");
    }

    [Fact]
    public async Task AMemberNeverCharged_ShouldSeeTheKraisCurrency()
    {
        _memberships.Setup(m => m.GetForMemberAsync(Person, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new MembershipRecord(Guid.NewGuid(), Kurin, 7, KurinBranch.UPYu, null, null, null, MembershipKind.Youth, DateTime.UtcNow.AddMonths(-1), null)
        ]);

        var result = await Handler().Handle(new GetMemberDuesQuery(Person), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.HasAccount.Should().BeFalse();
        result.Data.Currency.Should().Be("CZK");
        result.Data.NumberLocale.Should().Be("cs-CZ");
    }
}
