using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Tests.MeModule;

/// <summary>Whose dashboard gets проба, вмілості and точкування: a youth's, never a впорядник's.</summary>
public class CurrentMemberPersonTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Person = Guid.NewGuid();

    private static CurrentMember Reader(IReadOnlyList<PlastLevel> levels, params MembershipRecord[] memberships)
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(User);
        var directory = new Mock<IMembershipDirectory>();
        directory.Setup(m => m.GetCurrentForAccountAsync(User, It.IsAny<CancellationToken>())).ReturnsAsync(memberships);
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.FindByAccountAsync(User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(Person, User, Guid.NewGuid(), null, "Оксана", "Паливода", "o@x", null));
        members.Setup(m => m.GetLevelsAsync(Person, It.IsAny<CancellationToken>())).ReturnsAsync(levels);
        return new CurrentMember(user.Object, members.Object, directory.Object);
    }

    private static MembershipRecord In(KurinBranch branch, MembershipKind kind = MembershipKind.Youth) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 1, branch, null, Guid.NewGuid(), "Соколи", kind, DateTime.UtcNow, null);

    [Fact]
    public async Task AYouthInAYouthKurin_HasTheProgramme()
    {
        var me = await Reader([PlastLevel.Uchasnyk], In(KurinBranch.UPYu)).ReadPersonAsync(CancellationToken.None);

        me!.HasYouthProgram.Should().BeTrue();
    }

    [Fact]
    public async Task AVporiadnykWithASeniorStupin_DoesNot_EvenAsAYouthKurinsMember()
    {
        var me = await Reader([PlastLevel.Senior], In(KurinBranch.UPYu)).ReadPersonAsync(CancellationToken.None);

        me!.HasYouthProgram.Should().BeFalse();
    }

    [Fact]
    public async Task OneYouthKurinAmongSeniorOnes_IsEnough()
    {
        var me = await Reader([], In(KurinBranch.USP), In(KurinBranch.UPYu)).ReadPersonAsync(CancellationToken.None);

        me!.HasYouthProgram.Should().BeTrue();
        me.HasYouthProgramIn(me.Memberships[0]).Should().BeFalse();
        me.HasYouthProgramIn(me.Memberships[1]).Should().BeTrue();
    }

    [Fact]
    public async Task NobodyBehindTheAccount_IsNobody()
    {
        var members = new Mock<IMemberDirectory>();
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(User);

        (await new CurrentMember(user.Object, members.Object, Mock.Of<IMembershipDirectory>()).ReadPersonAsync(CancellationToken.None)).Should().BeNull();
    }
}
