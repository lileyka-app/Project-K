using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

/// <summary>Fifty tasks are not looked through by eye: the board searches, narrows, sorts and pages each column.</summary>
public class GetAgendaBoardHandlerTests
{
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _sokoly = Guid.NewGuid();
    private readonly Guid _vedmedi = Guid.NewGuid();
    private readonly List<AgendaItem> _items = [];
    private readonly GetAgendaBoardQueryHandler _handler;

    public GetAgendaBoardHandlerTests()
    {
        var agenda = new Mock<IAgendaItemRepository>();
        agenda.Setup(r => r.GetForViewerAsync(It.IsAny<AgendaViewerScope>(), null, null, false, AgendaItemKind.Task, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(() => _items);
        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.GetAllAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Group("Соколи", _kurinKey) { GroupKey = _sokoly }, new Group("Ведмеді", _kurinKey) { GroupKey = _vedmedi }]);
        var leaderships = new Mock<ILeadershipRepository>();
        leaderships.Setup(l => l.GetLeadershipRefsForKurinAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var categories = new Mock<IAgendaCategoryRepository>();
        categories.Setup(c => c.GetForKurinAsync(_kurinKey, true, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(u => u.AgendaItems).Returns(agenda.Object);
        uow.SetupGet(u => u.Groups).Returns(groups.Object);
        uow.SetupGet(u => u.Leaderships).Returns(leaderships.Object);
        uow.SetupGet(u => u.KurinTags).Returns(TestHelpers.NoKurinTags.Repository());
        uow.SetupGet(u => u.AgendaCategories).Returns(categories.Object);

        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.GetByKurinAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        members.Setup(m => m.GetByKurinAsync(_kurinKey, It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var access = new Mock<IAgendaAccess>();
        access.Setup(a => a.BuildViewerAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgendaViewerContext(_kurinKey, Guid.NewGuid(), null, null, [], [], true, true));

        var userManager = new Mock<UserManager<AppUser>>(Mock.Of<IUserStore<AppUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        _handler = new GetAgendaBoardQueryHandler(uow.Object, members.Object, access.Object, userManager.Object);
    }

    private AgendaItem Add(string title, Guid group, AgendaItemStatus status = AgendaItemStatus.Todo, DateTime? due = null)
    {
        var item = new AgendaItem
        {
            AgendaItemKey = Guid.NewGuid(),
            KurinKey = _kurinKey,
            Kind = AgendaItemKind.Task,
            Title = title,
            Status = status,
            EndUtc = due,
            Assignments = [new AgendaAssignment { TargetType = AgendaTargetType.Group, TargetKey = group, Status = status }]
        };
        _items.Add(item);
        return item;
    }

    private async Task<AgendaBoardResponse> Board(AgendaBoardFilter filter) =>
        (await _handler.Handle(new GetAgendaBoardQuery(_kurinKey, filter), CancellationToken.None)).Data!;

    private static List<string> Titles(AgendaBoardResponse board, AgendaItemStatus status) =>
        board.Columns.Single(c => c.Status == status).Items.Select(i => i.Title).ToList();

    [Fact]
    public async Task EachColumn_ComesWithItsFirstPage_AndHowManyItHolds()
    {
        for (var i = 0; i < 25; i++)
        {
            Add($"Задача {i:00}", _sokoly);
        }
        Add("Зроблена", _sokoly, AgendaItemStatus.Done);

        var board = await Board(new AgendaBoardFilter { Take = 20, Sort = AgendaBoardSort.Title });

        board.Columns.Single(c => c.Status == AgendaItemStatus.Todo).Total.Should().Be(25);
        Titles(board, AgendaItemStatus.Todo).Should().HaveCount(20);
        Titles(board, AgendaItemStatus.Done).Should().Equal("Зроблена");
    }

    [Fact]
    public async Task LoadingMore_ReturnsTheNextPageOfThatColumnOnly()
    {
        for (var i = 0; i < 25; i++)
        {
            Add($"Задача {i:00}", _sokoly);
        }

        var more = await Board(new AgendaBoardFilter { Status = AgendaItemStatus.Todo, Skip = 20, Take = 20, Sort = AgendaBoardSort.Title });

        more.Columns.Should().ContainSingle();
        Titles(more, AgendaItemStatus.Todo).Should().Equal("Задача 20", "Задача 21", "Задача 22", "Задача 23", "Задача 24");
    }

    [Fact]
    public async Task Search_FindsByTitle_IgnoringCase_AndByTheGurtokItIsFor()
    {
        Add("Здати вкладку", _sokoly);
        Add("Підготувати ватру", _vedmedi);

        Titles(await Board(new AgendaBoardFilter { Search = "ВКЛАДК" }), AgendaItemStatus.Todo).Should().Equal("Здати вкладку");
        Titles(await Board(new AgendaBoardFilter { Search = "ведмед" }), AgendaItemStatus.Todo).Should().Equal("Підготувати ватру");
    }

    [Fact]
    public async Task TheTargetFilter_NarrowsToOneGurtok_AndOffersOnlyWhatIsOnTheBoard()
    {
        Add("Здати вкладку", _sokoly);
        Add("Підготувати ватру", _vedmedi);

        var board = await Board(new AgendaBoardFilter { TargetType = AgendaTargetType.Group, TargetKey = _vedmedi });

        Titles(board, AgendaItemStatus.Todo).Should().Equal("Підготувати ватру");
        board.Targets.Select(t => t.Label).Should().BeEquivalentTo("Соколи", "Ведмеді");
    }

    [Fact]
    public async Task ByDeadline_TheNearestComesFirst_AndThoseWithoutOneLast()
    {
        Add("Без терміну", _sokoly);
        Add("Пізніше", _sokoly, due: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        Add("Скоро", _sokoly, due: new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc));

        Titles(await Board(new AgendaBoardFilter { Sort = AgendaBoardSort.Due }), AgendaItemStatus.Todo)
            .Should().Equal("Скоро", "Пізніше", "Без терміну");
    }
}
