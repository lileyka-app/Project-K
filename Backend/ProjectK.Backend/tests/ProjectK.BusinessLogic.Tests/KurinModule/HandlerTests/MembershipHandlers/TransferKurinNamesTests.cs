using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MembershipHandlers;

/// <summary>
/// A senior kurin asking for someone must not read as the УПЮ kurin of the same number: «5-й курінь
/// УСП», not «к. ч. 5».
/// </summary>
public class TransferKurinNamesTests
{
    [Fact]
    public void ASeniorKurin_IsNamedByItsGilky_AndAYouthKurinByItsNumber()
    {
        KurinBranchNames.InText(new Kurin(5, KurinBranch.USP)).Should().Be("5-й курінь УСП");
        KurinBranchNames.InText(Kurin.InBranches([new(KurinBranch.USP, 5), new(KurinBranch.UPS, 20)]))
            .Should().Be("5-й курінь УСП · 20-й курінь УПС");
        KurinBranchNames.InText(new Kurin(5)).Should().Be("к. ч. 5");
        KurinBranchNames.InText(new Kurin(null)).Should().Be("підг. курінь");
    }

    [Fact]
    public void ARequestsKurin_CarriesItsGilky()
    {
        var senior = new Kurin(5, KurinBranch.USP);

        var named = MembershipRequestKurin.Of(senior.KurinKey, senior);

        named.Number.Should().Be(5);
        named.Branches.Should().Equal(new KurinNumbering(KurinBranch.USP, 5));
        MembershipRequestKurin.Of(Guid.NewGuid(), null).Branches.Should().BeEmpty();
    }
}
