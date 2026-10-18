using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.KraiProfiles;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KraiModule;

/// <summary>What a kurin takes from its край's profile: the zone its week runs in, and the currency, number format and clock of its вкладка.</summary>
public class KurinLocaleTests
{
    private static readonly Guid Kurin = Guid.NewGuid();

    private static Mock<IKraiProfileProvider> Profile(string timeZone)
    {
        var profiles = new Mock<IKraiProfileProvider>();
        profiles.Setup(p => p.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(new KraiProfile(
            "cz", "Чехія", 1, new KraiCatalogSettings(["cz"], []), new KraiLocaleSettings("CZK", timeZone, "cs-CZ"), KraiCardFieldsSettings.Default, KraiDuesSettings.Default));
        return profiles;
    }

    [Fact]
    public async Task AKurin_CountsItsWeek_InItsKraisZone()
    {
        var zone = await Profile("Europe/Prague").Object.ForKurinAsync(Kurin, default);

        zone.Id.Should().Be("Europe/Prague");
    }

    [Fact]
    public async Task AnUnknownKurin_UsesTheInstanceZone_AndHryvnias()
    {
        var profiles = new Mock<IKraiProfileProvider>().Object;

        (await profiles.ForKurinAsync(Kurin, default)).Should().Be(AgendaRecurrence.DefaultZone);
        (await profiles.DuesKraiForKurinAsync(Kurin, default)).Should().Be(DuesKrai.Default);
    }

    [Fact]
    public async Task AKurin_CountsItsDues_InItsKraisCurrency_AndWritesThemItsWay()
    {
        var krai = await Profile("Europe/Prague").Object.DuesKraiForKurinAsync(Kurin, default);

        krai.Currency.Should().Be("CZK");
        krai.NumberLocale.Should().Be("cs-CZ");
        krai.Zone.Id.Should().Be("Europe/Prague");
    }

    [Fact]
    public async Task AKurinsQuarter_TurnsAtMidnightInItsKrai()
    {
        var prague = await Profile("Europe/Prague").Object.DuesKraiForKurinAsync(Kurin, default);
        var toronto = await Profile("America/Toronto").Object.DuesKraiForKurinAsync(Kurin, default);
        var lateOn31March = new DateTimeOffset(2026, 3, 31, 23, 30, 0, TimeSpan.Zero);

        prague.QuarterOf(lateOn31March).Should().Be(new Common.Models.Dues.DuesQuarter(2026, 2));
        toronto.QuarterOf(lateOn31March).Should().Be(new Common.Models.Dues.DuesQuarter(2026, 1));
        prague.Today(lateOn31March).Should().Be(new DateOnly(2026, 4, 1));
    }

    [Fact]
    public async Task ASeries_StaysAtItsLocalHour_AcrossTheKraisClockChange()
    {
        // 18:00 in Toronto on Monday 26 October 2026, a week before Canada's clocks go back.
        var zone = await Profile("America/Toronto").Object.ForKurinAsync(Kurin, default);
        var start = new DateTime(2026, 10, 26, 22, 0, 0, DateTimeKind.Unspecified);
        var item = new Common.Entities.KurinModule.Agenda.AgendaItem
        {
            Kind = Common.Models.Enums.AgendaItemKind.Event,
            StartUtc = start,
            RecurrenceFrequency = Common.Models.Enums.RecurrenceFrequency.Weekly,
            RecurrenceInterval = 1,
            RecurrenceByWeekday = 1 << (int)DayOfWeek.Monday
        };

        var occurrences = AgendaRecurrence.Expand(item, new DateTime(2026, 10, 25), new DateTime(2026, 11, 4), zone).ToList();

        occurrences.Select(o => o.StartUtc.Hour).Should().Equal(22, 23);
    }
}
