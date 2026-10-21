using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

/// <summary>Someone who left the kurin with a debt is still named in its dues, but not offered in its pickers.</summary>
public class DuesNamesTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Group = Guid.NewGuid();

    private static readonly KurinMembershipRecord Staying =
        new(Guid.NewGuid(), Guid.NewGuid(), Group, MembershipKind.Youth, new DateTime(2025, 9, 1), null);

    private static readonly KurinMembershipRecord Left =
        new(Guid.NewGuid(), Guid.NewGuid(), Group, MembershipKind.Youth, new DateTime(2024, 9, 1), new DateTime(2026, 6, 1));

    private readonly Mock<IMemberDirectory> _members = new();

    private Task<DuesNames> Load()
    {
        var memberships = new Mock<IMembershipDirectory>();
        memberships.Setup(m => m.GetInKurinAsync(Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Staying, Left]);
        _members.Setup(m => m.GetByKurinAsync(Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Person(Staying.MemberKey, "Остап", "Бондаренко")]);
        return DuesNames.LoadAsync(memberships.Object, _members.Object, Kurin, CancellationToken.None);
    }

    private static MemberSummary Person(Guid memberKey, string first, string last) =>
        new(memberKey, Guid.NewGuid(), Guid.Empty, null, first, last, $"{memberKey}@example.org", null);

    [Fact]
    public async Task Names_someone_who_left_from_their_member_record()
    {
        _members.Setup(m => m.GetByKeysAsync(
                It.Is<IReadOnlyCollection<Guid>>(keys => keys.Count == 1 && keys.Contains(Left.MemberKey)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([Person(Left.MemberKey, "Андрій", "Мельник")]);

        var names = await Load();

        names.OfMembership(Left.MembershipKey).Should().Be("Андрій Мельник");
        names.OfMembership(Staying.MembershipKey).Should().Be("Остап Бондаренко");
    }

    [Fact]
    public async Task Offers_only_those_who_stand_in_the_kurin_now()
    {
        _members.Setup(m => m.GetByKeysAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Person(Left.MemberKey, "Андрій", "Мельник")]);

        var names = await Load();

        names.People.Select(p => p.MemberKey).Should().Equal(Staying.MemberKey);
    }

    [Fact]
    public async Task Says_a_dash_for_a_member_record_that_is_gone()
    {
        _members.Setup(m => m.GetByKeysAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var names = await Load();

        names.OfMembership(Left.MembershipKey).Should().Be("—");
    }
}
