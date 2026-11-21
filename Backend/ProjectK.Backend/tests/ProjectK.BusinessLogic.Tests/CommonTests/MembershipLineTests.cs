using FluentAssertions;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

/// <summary>In a kurin of both senior гілки, the person's ступінь says which one they stand in.</summary>
public class MembershipLineTests
{
    private static MembershipRecord InOrden(params KurinNumbering[] branches) => new(
        Guid.NewGuid(), Guid.NewGuid(), 5, KurinBranch.USP, "Сторожі Горган", null, null,
        MembershipKind.Youth, DateTime.UtcNow, null, branches);

    [Fact]
    public void ASenior_StandsInTheUps_OfAKurinWhoseMainBranchIsUsp()
    {
        var line = InOrden(new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 20))
            .InLineOf([PlastLevel.Starshoplastun, PlastLevel.Senior]);

        line.Branch.Should().Be(KurinBranch.UPS);
        line.KurinNumber.Should().Be(20);
    }

    [Fact]
    public void AStarshyiPlastun_StaysInTheUsp() =>
        InOrden(new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 20))
            .InLineOf([PlastLevel.Starshoplastun]).Branch.Should().Be(KurinBranch.USP);

    [Fact]
    public void ASenior_InAKurinWithoutAUps_KeepsTheKurinsBranch() =>
        InOrden(new KurinNumbering(KurinBranch.USP, 5))
            .InLineOf([PlastLevel.Senior]).Branch.Should().Be(KurinBranch.USP);

    [Fact]
    public void AYouthKurin_IsNeverRenamed_ByASeniorStupin()
    {
        var youth = new MembershipRecord(
            Guid.NewGuid(), Guid.NewGuid(), 1, KurinBranch.UPYu, null, null, null,
            MembershipKind.Staff, DateTime.UtcNow, null, [new(KurinBranch.UPYu, 1)]);

        youth.InLineOf([PlastLevel.Senior]).Branch.Should().Be(KurinBranch.UPYu);
    }
}
