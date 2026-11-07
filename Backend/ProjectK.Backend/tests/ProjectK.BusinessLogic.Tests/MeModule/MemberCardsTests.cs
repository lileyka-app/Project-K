using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Card;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Catalog;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.MeModule;

/// <summary>
/// Who may share a card and what it shows: 18 or older decides alone; younger only as far as every
/// kurin they stand in allows; and a scanned card is read live, never from what was allowed before.
/// </summary>
public class MemberCardsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    private readonly Guid _person = Guid.NewGuid();
    private readonly Guid _kurinA = Guid.NewGuid();
    private readonly Guid _kurinB = Guid.NewGuid();

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMembershipRepository> _memberships = new();
    private readonly Mock<IKurinCardSharingRepository> _kurinSharing = new();
    private readonly Mock<IMemberCardShareRepository> _shares = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly Mock<ICardTokenSigner> _signer = new();
    private readonly Mock<IResourceScopeReader> _scope = new();
    private readonly Mock<IHonoursCatalog> _honours = new();
    private readonly List<KurinCardSharing> _settings = [];
    private readonly List<Membership> _standing = [];
    private MemberCardShare? _share;

    public MemberCardsTests()
    {
        _unitOfWork.SetupGet(u => u.Memberships).Returns(_memberships.Object);
        _unitOfWork.SetupGet(u => u.KurinCardSharing).Returns(_kurinSharing.Object);
        _unitOfWork.SetupGet(u => u.MemberCardShares).Returns(_shares.Object);
        _memberships.Setup(r => r.GetActiveForMemberAsync(_person, It.IsAny<CancellationToken>())).ReturnsAsync(() => _standing);
        _memberships.Setup(r => r.GetRecordsForMemberAsync(_person, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _standing.Select(m => new MembershipRecord(m.MembershipKey, m.KurinKey, 15, KurinBranch.UPYu, null, null, "Соколи", m.Kind, m.JoinedAtUtc, null)).ToList());
        _kurinSharing.Setup(r => r.GetForKurinsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> keys, CancellationToken _) => _settings.Where(s => keys.Contains(s.KurinKey)).ToList());
        _shares.Setup(r => r.GetForMemberAsync(_person, It.IsAny<CancellationToken>())).ReturnsAsync(() => _share);
        _members.Setup(m => m.FindAsync(_person, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(_person, null, _kurinA, null, "Назар", "Коваль", "n@x", "photo.jpg"));
        _members.Setup(m => m.GetLevelsAsync(_person, It.IsAny<CancellationToken>())).ReturnsAsync([PlastLevel.Uchasnyk]);
    }

    private void Born(int yearsAgo) =>
        _members.Setup(m => m.GetDateOfBirthAsync(_person, It.IsAny<CancellationToken>())).ReturnsAsync(Today.AddYears(-yearsAgo));

    private void StandsIn(Guid kurinKey) =>
        _standing.Add(new Membership { MemberKey = _person, KurinKey = kurinKey, JoinedAtUtc = Now.UtcDateTime.AddYears(-2) });

    private void Allows(Guid kurinKey, CardField fields, bool link = false) =>
        _settings.Add(new KurinCardSharing { KurinKey = kurinKey, MinorsMayShare = true, MinorFields = fields, MinorsMayLink = link });

    private MemberCards Cards() => new(
        _unitOfWork.Object,
        _members.Object,
        Mock.Of<IMemberProgressDirectory>(p => p.GetForMemberAsync(_person, It.IsAny<CancellationToken>()) == Task.FromResult(MemberProgress.Empty)),
        Mock.Of<IProbesCatalogService>(),
        Mock.Of<IBadgesCatalogService>(),
        new FakeBlobReadLinks(),
        _scope.Object,
        _honours.Object,
        Options.Create(new EmailSettings { BaseUrl = "https://lileyka.test" }),
        new FixedTimeProvider(Now));

    private ReadCheckedCardQueryHandler Checked() => new(Cards(), _signer.Object, new FixedTimeProvider(Now));

    private ReadLinkedCardQueryHandler Linked() => new(Cards(), _unitOfWork.Object);

    [Fact]
    public async Task SomeoneOf18_SharesEverything_WithoutTheKurin()
    {
        Born(18);
        StandsIn(_kurinA);

        var allowance = await Cards().AllowanceAsync(_person, default);

        allowance.Should().Be(new Modules.MeModule.Models.CardAllowance(true, true, CardField.All, true));
    }

    [Fact]
    public async Task AMinor_SharesNothing_UntilTheKurinAllows()
    {
        Born(14);
        StandsIn(_kurinA);

        (await Cards().AllowanceAsync(_person, default)).MayShare.Should().BeFalse();

        Allows(_kurinA, CardField.Kurin | CardField.Awards);
        var allowance = await Cards().AllowanceAsync(_person, default);
        allowance.MayShare.Should().BeTrue();
        allowance.MaxFields.Should().Be(CardField.Kurin | CardField.Awards);
        allowance.MayLink.Should().BeFalse();
    }

    [Fact]
    public async Task AMinorInTwoKurins_NeedsBoth_AndGetsWhatBothAllow()
    {
        Born(15);
        StandsIn(_kurinA);
        StandsIn(_kurinB);
        Allows(_kurinA, CardField.Kurin | CardField.Awards | CardField.Photo, link: true);

        (await Cards().AllowanceAsync(_person, default)).MayShare.Should().BeFalse();

        Allows(_kurinB, CardField.Kurin | CardField.Awards, link: false);
        var allowance = await Cards().AllowanceAsync(_person, default);
        allowance.MaxFields.Should().Be(CardField.Kurin | CardField.Awards);
        allowance.MayLink.Should().BeFalse();
    }

    [Fact]
    public async Task NoDateOfBirth_CountsAsAMinor()
    {
        _members.Setup(m => m.GetDateOfBirthAsync(_person, It.IsAny<CancellationToken>())).ReturnsAsync((DateOnly?)null);
        StandsIn(_kurinA);

        (await Cards().AllowanceAsync(_person, default)).MayShare.Should().BeFalse();
    }

    [Fact]
    public async Task ACheckedCard_ShowsOnlyWhatIsBothChosenAndAllowed_StampedWithTheTime()
    {
        Born(14);
        StandsIn(_kurinA);
        Allows(_kurinA, CardField.Kurin | CardField.Level);
        _share = new MemberCardShare { MemberKey = _person, Fields = CardField.Photo | CardField.Kurin };
        _signer.Setup(s => s.Read("t", Now.UtcDateTime)).Returns(_person);

        var card = (await Checked().Handle(new ReadCheckedCardQuery("t"), default)).Data!;

        card.FullName.Should().Be("Назар Коваль");
        card.PhotoUrl.Should().BeNull();
        card.Kurins.Should().ContainSingle().Which.Number.Should().Be(15);
        card.Level.Should().BeNull();
        card.CheckedAtUtc.Should().Be(Now.UtcDateTime);
    }

    [Fact]
    public async Task ACheckedCard_IsGone_OnceTheKurinWithdraws()
    {
        Born(14);
        StandsIn(_kurinA);
        _signer.Setup(s => s.Read("t", Now.UtcDateTime)).Returns(_person);

        var result = await Checked().Handle(new ReadCheckedCardQuery("t"), default);

        result.Type.Should().Be(ResultType.NotFound);
    }

    [Fact]
    public async Task AnExpiredOrForgedToken_IsNotFound()
    {
        var result = await Checked().Handle(new ReadCheckedCardQuery("forged"), default);

        result.Type.Should().Be(ResultType.NotFound);
    }

    [Fact]
    public async Task ALinkedCard_IsGone_WhenLinksAreNotAllowed()
    {
        Born(14);
        StandsIn(_kurinA);
        Allows(_kurinA, CardField.Kurin, link: false);
        _shares.Setup(r => r.GetByLinkTokenAsync("link", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberCardShare { MemberKey = _person, LinkToken = "link" });

        (await Linked().Handle(new ReadLinkedCardQuery("link"), default)).Type.Should().Be(ResultType.NotFound);

        _settings[0].MinorsMayLink = true;
        (await Linked().Handle(new ReadLinkedCardQuery("link"), default)).Type.Should().Be(ResultType.Success);
    }

    [Fact]
    public void CardFields_GoOutAsANumber_EvenWhereEnumsAreWrittenByName()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

        var json = System.Text.Json.JsonSerializer.Serialize(
            new Modules.MeModule.Models.CardAllowance(true, false, CardField.Kurin | CardField.Level, false), options);

        json.Should().Contain("\"maxFields\":6");
    }

    private void Earned()
    {
        _members.Setup(m => m.GetConfirmedAwardsAsync(_person, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ConfirmedAward(null, "usp-hramota-zaslugy", new DateTime(2025, 5, 1)),
            new ConfirmedAward(MemberAwardLevel.Second, null, new DateTime(2019, 5, 1))
        ]);
        _honours.Setup(h => h.GetById("usp-hramota-zaslugy"))
            .Returns(new CatalogHonour("usp-hramota-zaslugy", HonourKind.Award, "Грамота заслуги", "usp-vidznachennia", "usp", 0, "https://catalog.test/hramota.png"));
    }

    [Fact]
    public async Task AYouth_ShowsTheirUpyuAwards_ByLevel()
    {
        Born(18);
        Earned();

        var card = await Cards().ReadAsync(_person, CardField.Awards, null, default);

        card!.Awards.Should().Equal(new Modules.MeModule.Models.PublicCardAward(2, "2-е відзначення", null, new DateTime(2019, 5, 1)));
    }

    [Fact]
    public async Task ASenior_ShowsTheSeniorAwards_WithTheCatalogueTitleAndPicture_NotTheYouthOnes()
    {
        Born(25);
        Earned();
        _scope.Setup(s => s.StandsInSeniorKurinAsync(ResourceType.Member, _person, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var card = await Cards().ReadAsync(_person, CardField.Awards, null, default);

        card!.Awards.Should().Equal(new Modules.MeModule.Models.PublicCardAward(null, "Грамота заслуги", "https://catalog.test/hramota.png", new DateTime(2025, 5, 1)));
    }
}
