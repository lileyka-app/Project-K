using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KraiModule;

public class KraiProfileProviderTests
{
    private readonly Mock<IKraiRepository> _krais = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly KraiProfileProvider _provider;

    public KraiProfileProviderTests()
    {
        var cache = new Mock<IBackendCache>();
        cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<CachePolicy>(),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<Common.Models.KraiProfiles.KraiProfile?>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<CacheScopeContext?>()))
            .Returns((CachePolicy _, string _, Func<CancellationToken, Task<Common.Models.KraiProfiles.KraiProfile?>> factory, CancellationToken token, CacheScopeContext? _) => factory(token));

        _unitOfWork.Setup(u => u.Krais).Returns(_krais.Object);
        _unitOfWork.Setup(u => u.Kurins).Returns(_kurins.Object);
        _provider = new KraiProfileProvider(_unitOfWork.Object, cache.Object, NullLogger<KraiProfileProvider>.Instance);
    }

    private void Given(string code, string? json, int revision = 1)
    {
        _krais.Setup(r => r.GetAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Krai { Code = code, Name = code.ToUpperInvariant() });
        _krais.Setup(r => r.GetLatestProfileRevisionAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(json is null ? null : new KraiProfileRevision { KraiCode = code, Revision = revision, Json = json });
    }

    [Fact]
    public async Task AnEmptyProfile_GivesTheValuesInCode()
    {
        Given("ua", "{}");

        var profile = await _provider.GetForKraiAsync("ua");

        profile!.Revision.Should().Be(1);
        profile.Catalog.BadgeSources.Should().Equal("ua");
        profile.Catalog.HiddenBadges.Should().BeEmpty();
        profile.Locale.Currency.Should().Be("UAH");
        profile.Locale.TimeZone.Should().Be(AgendaRecurrence.DefaultZone.Id);
        profile.Locale.NumberLocale.Should().Be("uk-UA");
    }

    [Fact]
    public async Task SavedValues_OverrideTheValuesInCode()
    {
        Given("cz", """
            {
              "catalog": { "badgeSources": [" CZ ", "ua", "cz"], "hiddenBadges": [" b1 ", "", "b1"] },
              "locale": { "currency": "czk", "timeZone": "Europe/Prague" }
            }
            """, revision: 3);

        var profile = await _provider.GetForKraiAsync("cz");

        profile!.Revision.Should().Be(3);
        profile.Catalog.BadgeSources.Should().Equal("cz", "ua");
        profile.Catalog.HiddenBadges.Should().Equal("b1");
        profile.Locale.Currency.Should().Be("CZK");
        profile.Locale.TimeZone.Should().Be("Europe/Prague");
        profile.Locale.NumberLocale.Should().Be("uk-UA");
    }

    [Fact]
    public async Task CardFields_AreTheCardAsItWas_WithTheKraisChangesOverIt()
    {
        Given("cz", """
            {
              "cardFields": {
                "fields": { "middleName": "hidden", "school": "hidden", "shoeSize": "hidden" },
                "extra": [
                  { "key": "visaType", "label": "Тип візи", "required": true },
                  { "key": "visaUntil", "label": "Віза до", "type": "date" },
                  { "key": "odd", "label": "Невідомий тип", "type": "number" }
                ]
              }
            }
            """);

        var card = (await _provider.GetForKraiAsync("cz"))!.CardFields;

        card.ModeOf(KraiCardFields.MiddleName).Should().Be(KraiCardFields.Hidden);
        card.ModeOf(KraiCardFields.PhoneNumber).Should().Be(KraiCardFields.Required);
        card.ModeOf(KraiCardFields.School).Should().Be(KraiCardFields.Hidden);
        card.Fields.Should().NotContainKey("shoeSize");
        card.Extra.Select(field => field.Key).Should().Equal("visaType", "visaUntil");
        card.Extra[0].Required.Should().BeTrue();
    }

    [Fact]
    public async Task BrokenJson_FallsBackToTheValuesInCode()
    {
        Given("cz", "{ not json");

        var profile = await _provider.GetForKraiAsync("cz");

        profile!.Catalog.BadgeSources.Should().Equal("cz");
        profile.Locale.Currency.Should().Be("UAH");
    }

    [Fact]
    public async Task AKraiWithoutRevisions_HasRevisionZero()
    {
        Given("cz", null);

        var profile = await _provider.GetForKraiAsync("cz");

        profile!.Revision.Should().Be(0);
        profile.Catalog.BadgeSources.Should().Equal("cz");
    }

    [Fact]
    public async Task AnUnknownKrai_HasNoProfile()
    {
        (await _provider.GetForKraiAsync("xx")).Should().BeNull();
    }

    [Fact]
    public async Task AKurin_GetsTheProfileOfItsKrai()
    {
        Given("cz", "{}");
        var kurin = new Kurin(5) { KraiCode = "cz" };
        _kurins.Setup(r => r.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(kurin);

        var result = await new GetKurinProfileQueryHandler(_provider).Handle(new GetKurinProfileQuery(kurin.KurinKey), default);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.KraiCode.Should().Be("cz");
    }

    private const string PragueCard = """
        {
          "cardFields": {
            "fields": { "middleName": "optional" },
            "extra": [
              { "key": "visaType", "label": "Тип візи", "required": true },
              { "key": "note", "label": "Примітка" }
            ]
          }
        }
        """;

    [Fact]
    public async Task AKurin_NarrowsItsKraisCard_ButKeepsWhatTheKraiRequires()
    {
        Given("cz", PragueCard);
        var kurin = new Kurin(5) { KraiCode = "cz", HiddenCardFields = """["middleName","note","visaType","phoneNumber"]""" };
        _kurins.Setup(r => r.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(kurin);

        var card = (await _provider.GetForKurinAsync(kurin.KurinKey))!.CardFields;

        card.ModeOf(KraiCardFields.MiddleName).Should().Be(KraiCardFields.Hidden);
        card.ModeOf(KraiCardFields.PhoneNumber).Should().Be(KraiCardFields.Required);
        card.Extra.Select(field => field.Key).Should().Equal("visaType");
        (await _provider.GetForKraiAsync("cz"))!.CardFields.Extra.Should().HaveCount(2);
    }

    [Fact]
    public async Task AKurin_HidesOnlyWhatItMay()
    {
        Given("cz", PragueCard);
        var kurin = new Kurin(5) { KraiCode = "cz" };
        _kurins.Setup(r => r.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(kurin);
        var handler = new SetKurinCardFieldsCommandHandler(_unitOfWork.Object, _provider, new Mock<IBackendCache>().Object);

        var result = await handler.Handle(new SetKurinCardFieldsCommand(kurin.KurinKey, ["note", "visaType", "email"]), default);

        kurin.HiddenCardFields.Should().Be("""["note"]""");
        result.Data!.Should().Equal(
            new KurinCardFieldResponse("middleName", "По батькові", false),
            new KurinCardFieldResponse("address", "Адреса", false),
            new KurinCardFieldResponse("school", "Школа", false),
            new KurinCardFieldResponse("parents", "Контакти батьків", false),
            new KurinCardFieldResponse("note", "Примітка", true));
    }

    [Fact]
    public async Task AMissingKurin_IsNotFound()
    {
        var result = await new GetKurinProfileQueryHandler(_provider).Handle(new GetKurinProfileQuery(Guid.NewGuid()), default);

        result.Type.Should().Be(ResultType.NotFound);
        result.ErrorCode.Should().Be("KurinNotFound");
    }

    [Fact]
    public async Task Concessions_KeepOnlyTheRulesItCanWorkOut()
    {
        Given("cz", """
            {
              "dues": {
                "concessions": [
                  { "key": "sibling", "name": "Брат чи сестра", "kind": "percent", "value": 20, "appliesTo": ["stanytsia", "kurin"] },
                  { "key": "free", "name": "Звільнення", "kind": "exempt", "appliesTo": ["group"] },
                  { "key": "broken", "name": "Зламана", "kind": "percent", "value": 150, "appliesTo": ["kurin"] },
                  { "key": "nothing", "name": "Ні на що", "kind": "amount", "value": 10, "appliesTo": ["moon"] }
                ]
              }
            }
            """);

        var concessions = (await _provider.GetForKraiAsync("cz"))!.Dues.Concessions;

        concessions.Select(c => c.Key).Should().Equal("sibling", "free");
        concessions[0].Parts.Should().Be(Common.Models.Enums.DuesParts.Stanytsia | Common.Models.Enums.DuesParts.Kurin);
        concessions[0].Value.Should().Be(20);
    }
}
