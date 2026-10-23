using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

/// <summary>An event on the calendar only through the kurin's schedule is there to look at.</summary>
public class AgendaAudienceTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Sokoly = Guid.NewGuid();
    private static readonly Guid Kelty = Guid.NewGuid();
    private static readonly AgendaCategory Skhodyny = new() { AgendaCategoryKey = Guid.NewGuid(), KurinKey = Kurin, Name = "Сходини", IsKurinSchedule = true };

    private static AgendaItem Event(Guid group) => new()
    {
        AgendaItemKey = Guid.NewGuid(),
        KurinKey = Kurin,
        Kind = AgendaItemKind.Event,
        Title = "Сходини",
        AgendaCategoryKey = Skhodyny.AgendaCategoryKey,
        CreatedByUserKey = Guid.NewGuid(),
        Assignments = [new AgendaAssignment { TargetType = AgendaTargetType.Group, TargetKey = group }]
    };

    private static AgendaItemResponse Response(AgendaItem item, AgendaViewerContext viewer) =>
        AgendaItemResponseFactory.Create(item, viewer, "Весь курінь",
            new Dictionary<Guid, string> { [Sokoly] = "Соколи", [Kelty] = "Кельти" },
            new Dictionary<Guid, string>(), new Dictionary<Guid, string>(), new Dictionary<Guid, string>(),
            new Dictionary<Guid, AgendaCategory> { [Skhodyny.AgendaCategoryKey] = Skhodyny },
            AgendaRoster.Empty);

    private static AgendaViewerContext YouthOfSokoly() => new(Kurin, Guid.NewGuid(), Guid.NewGuid(), Sokoly, [Sokoly], [], false, false);

    [Fact]
    public void AnotherGurtoksSkhodyny_AreOnTheSchedule_ReadOnly()
    {
        var response = Response(Event(Kelty), YouthOfSokoly());

        response.Audience.Should().Be(AgendaAudience.Schedule);
        response.IsKurinSchedule.Should().BeTrue();
        response.CanEdit.Should().BeFalse();
    }

    [Fact]
    public void OnesOwnSkhodyny_AreAssigned_EvenThoughTheGroupIsTheSchedule()
    {
        Response(Event(Sokoly), YouthOfSokoly()).Audience.Should().Be(AgendaAudience.Assigned);
    }

    // The switch «Графіки гуртків» hides only Schedule rows, so the cases below are what it never hides.

    [Fact]
    public void AnotherGurtoksSkhodyny_RaisedByTheViewer_AreAssigned()
    {
        var viewer = YouthOfSokoly();
        var item = Event(Kelty);
        item.CreatedByUserKey = viewer.ViewerUserKey!.Value;

        Response(item, viewer).Audience.Should().Be(AgendaAudience.Assigned);
    }

    [Fact]
    public void SkhodynyOfAGurtokTheViewerLeads_AreAssigned()
    {
        var mentorOfKelty = new AgendaViewerContext(Kurin, Guid.NewGuid(), Guid.NewGuid(), Sokoly, [Sokoly, Kelty], [], false, true, [Kelty]);

        Response(Event(Kelty), mentorOfKelty).Audience.Should().Be(AgendaAudience.Assigned);
    }

    [Fact]
    public void SkhodynyAimedAlsoAtTheWholeKurin_AreAssigned()
    {
        var item = Event(Kelty);
        item.Assignments.Add(new AgendaAssignment { TargetType = AgendaTargetType.Kurin, TargetKey = Kurin });

        Response(item, YouthOfSokoly()).Audience.Should().Be(AgendaAudience.Assigned);
    }

    [Fact]
    public void AnotherGurtoksSkhodyny_ForAWholeKurinViewerWhoDidNotRaiseThem_AreOnTheSchedule()
    {
        var zvyazkovyi = new AgendaViewerContext(Kurin, Guid.NewGuid(), Guid.NewGuid(), null, [], [], true, true);

        Response(Event(Kelty), zvyazkovyi).Audience.Should().Be(AgendaAudience.Schedule);
    }

    [Fact]
    public void AGroupEventOutsideTheSchedule_IsAssigned_ForAWholeKurinViewer()
    {
        var zvyazkovyi = new AgendaViewerContext(Kurin, Guid.NewGuid(), Guid.NewGuid(), null, [], [], true, true);
        var item = Event(Kelty);
        item.AgendaCategoryKey = null;

        Response(item, zvyazkovyi).Audience.Should().Be(AgendaAudience.Assigned);
    }
}
