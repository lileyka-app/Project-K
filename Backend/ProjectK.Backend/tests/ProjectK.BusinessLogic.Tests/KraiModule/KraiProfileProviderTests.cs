using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.AgendaModule;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.BusinessLogic.Tests.KraiModule;

public class KraiProfileProviderTests
{
    private readonly Mock<IKraiRepository> _krais = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly KraiProfileDirectory _provider;

    public KraiProfileProviderTests()
    {
        var cache = new Mock<IBackendCache>();
        cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<CachePolicy>(),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<Common.Dtos.KraiModule.KraiProfile?>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<CacheScopeContext?>()))
            .Returns((CachePolicy _, string _, Func<CancellationToken, Task<Common.Dtos.KraiModule.KraiProfile?>> factory, CancellationToken token, CacheScopeContext? _) => factory(token));

        _unitOfWork.Setup(u => u.Krais).Returns(_krais.Object);
        _unitOfWork.Setup(u => u.Kurins).Returns(_kurins.Object);
        _provider = new KraiProfileDirectory(_unitOfWork.Object, cache.Object, NullLogger<KraiProfileDirectory>.Instance);
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
        profile.Locale.TimeZone.Should().Be(AgendaRecurrenceRules.DefaultZone.Id);
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

        card.ModeOf(KraiCardFieldNames.MiddleName).Should().Be(KraiCardFieldNames.Hidden);
        card.ModeOf(KraiCardFieldNames.PhoneNumber).Should().Be(KraiCardFieldNames.Required);
        card.ModeOf(KraiCardFieldNames.School).Should().Be(KraiCardFieldNames.Hidden);
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

        card.ModeOf(KraiCardFieldNames.MiddleName).Should().Be(KraiCardFieldNames.Hidden);
        card.ModeOf(KraiCardFieldNames.PhoneNumber).Should().Be(KraiCardFieldNames.Required);
        card.Extra.Select(field => field.Key).Should().Equal("visaType");
        (await _provider.GetForKraiAsync("cz"))!.CardFields.Extra.Should().HaveCount(2);
    }

    [Fact]
    public async Task AKurin_HidesOnlyWhatItMay()
    {
        Given("cz", PragueCard);
        var kurin = new Kurin(5) { KraiCode = "cz" };
        _kurins.Setup(r => r.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(kurin);
        var journal = new JournalRecorder(Mock.Of<ICurrentUserContext>());
        var handler = new SetKurinCardFieldsCommandHandler(_unitOfWork.Object, _provider, new Mock<IBackendCache>().Object, journal.Audit);

        var result = await handler.Handle(new SetKurinCardFieldsCommand(kurin.KurinKey, ["note", "visaType", "email"]), default);

        kurin.HiddenCardFields.Should().Be("""["note"]""");
        result.Data!.Should().Equal(
            new KurinCardFieldDto("middleName", "По батькові", false),
            new KurinCardFieldDto("address", "Адреса", false),
            new KurinCardFieldDto("school", "Школа", false),
            new KurinCardFieldDto("parents", "Контакти батьків", false),
            new KurinCardFieldDto("note", "Примітка", true));

        // Filed under the kurin itself, so an unscoped admin's change is kept too.
        var entry = journal.Entries.Should().ContainSingle().Subject;
        entry.Area.Should().Be(AuditArea.Settings);
        entry.Action.Should().Be(AuditActionNames.Settings.CardFieldsChanged);
        entry.KurinKey.Should().Be(kurin.KurinKey);
        entry.SubjectKey.Should().Be(kurin.KurinKey);
        entry.Changes.Should().Equal(new AuditChange("hiddenFields", null, "Примітка"));

        await handler.Handle(new SetKurinCardFieldsCommand(kurin.KurinKey, ["note"]), default);
        journal.Entries.Should().ContainSingle("saving the same fields again changes nothing");
    }

    /// <summary>A card field can share its key with private data; hiding it is a setting, not that data.</summary>
    [Fact]
    public async Task HidingAFieldNamedLikePrivateData_IsRecordedWithItsName()
    {
        Given("cz", """{ "cardFields": { "fields": { "address": "optional", "school": "optional" } } }""");
        var kurin = new Kurin(5) { KraiCode = "cz", HiddenCardFields = """["school"]""" };
        _kurins.Setup(r => r.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(kurin);
        var journal = new JournalRecorder(Mock.Of<ICurrentUserContext>());
        var handler = new SetKurinCardFieldsCommandHandler(_unitOfWork.Object, _provider, new Mock<IBackendCache>().Object, journal.Audit);

        await handler.Handle(new SetKurinCardFieldsCommand(kurin.KurinKey, ["address", "school"]), default);

        journal.Entries.Single().Changes.Should().Equal(new AuditChange("hiddenFields", "Школа", "Адреса, Школа"));
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
        concessions[0].Parts.Should().Be(Common.Enums.DuesModule.DuesParts.Stanytsia | Common.Enums.DuesModule.DuesParts.Kurin);
        concessions[0].Value.Should().Be(20);
    }
}
