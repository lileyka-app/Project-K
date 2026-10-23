using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Create;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

public class CreateAgendaItemHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMemberDirectory> _memberDirectory = new();
    private readonly Mock<IAgendaAccess> _access = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IDomainEventPublisher> _events = new();
    private readonly Mock<IAgendaItemRepository> _agendaRepo = new();
    private readonly Mock<IMemberRepository> _memberRepo = new();
    private readonly CreateAgendaItemCommandHandler _handler;
    private readonly JournalRecorder _journal;

    public CreateAgendaItemHandlerTests()
    {
        _uow.Setup(u => u.AgendaItems).Returns(_agendaRepo.Object);
        _memberDirectory.Setup(r => r.GetByKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MemberSummary>());
        _memberDirectory.Setup(r => r.GetByKurinAsync(It.IsAny<Guid>(), It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MemberSummary>());
        _uow.Setup(u => u.Groups).Returns(Mock.Of<IGroupRepository>());
        _journal = new JournalRecorder(_currentUser.Object);
        _handler = new CreateAgendaItemCommandHandler(_uow.Object, _memberDirectory.Object, _access.Object, _currentUser.Object, _events.Object, _journal.Audit);
    }

    private static CreateAgendaItemCommand CommandWith(Guid kurinKey, params AgendaTargetInput[] targets) => new()
    {
        KurinKey = kurinKey,
        Kind = AgendaItemKind.Task,
        Title = "Прибирання",
        Targets = targets.ToList()
    };

    [Fact]
    public async Task Handle_WhenActorMissing_ReturnsUnauthorized()
    {
        _currentUser.Setup(c => c.UserId).Returns((Guid?)null);

        var result = await _handler.Handle(CommandWith(Guid.NewGuid()), default);

        result.Type.Should().Be(ResultType.Unauthorized);
        _agendaRepo.Verify(r => r.Create(It.IsAny<AgendaItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAnyTargetDenied_ReturnsForbiddenAndDoesNotSave()
    {
        var kurinKey = Guid.NewGuid();
        var group = new AgendaTargetInput { TargetType = AgendaTargetType.Group, TargetKey = Guid.NewGuid() };
        _currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());
        _access.Setup(a => a.AuthorizeTargetAsync(group, ResourceAction.Create, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("Mentor has access only to assigned groups."));

        var result = await _handler.Handle(CommandWith(kurinKey, group), default);

        result.Type.Should().Be(ResultType.Forbidden);
        result.ErrorCode.Should().Be("AGENDA_TARGET_FORBIDDEN");
        _agendaRepo.Verify(r => r.Create(It.IsAny<AgendaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _journal.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RecordsWhatTheItemWasMadeWith_TargetsByName()
    {
        var kurinKey = Guid.NewGuid();
        var group = new Group("Соколи", kurinKey);
        var memberKey = Guid.NewGuid();
        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.GetAllAsync(kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([group]);
        _uow.Setup(u => u.Groups).Returns(groups.Object);
        _memberDirectory.Setup(r => r.GetByKurinAsync(kurinKey, It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberSummary(memberKey, null, kurinKey, null, "Олесь", "Юнак", "o@x", null)]);
        _currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());
        _access.Setup(a => a.AuthorizeTargetAsync(It.IsAny<AgendaTargetInput>(), ResourceAction.Create, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());
        var command = new CreateAgendaItemCommand
        {
            KurinKey = kurinKey,
            Kind = AgendaItemKind.Event,
            Title = "Табір",
            Location = "Карпати",
            StartUtc = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc),
            Targets =
            [
                new AgendaTargetInput { TargetType = AgendaTargetType.Member, TargetKey = memberKey },
                new AgendaTargetInput { TargetType = AgendaTargetType.Group, TargetKey = group.GroupKey },
                new AgendaTargetInput { TargetType = AgendaTargetType.Kurin, TargetKey = kurinKey }
            ]
        };

        var result = await _handler.Handle(command, default);

        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Area.Should().Be(AuditArea.Agenda);
        entry.Action.Should().Be(AuditActions.Agenda.Created);
        entry.KurinKey.Should().Be(kurinKey);
        entry.SubjectKey.Should().Be(result.Data);
        entry.SubjectLabel.Should().Be("Табір");
        entry.Changes.Should().Equal(
            new AuditChange("title", null, "Табір"),
            new AuditChange("kind", null, "Event"),
            new AuditChange("start", null, "2026-08-10"),
            new AuditChange("end", null, "2026-08-14"),
            new AuditChange("location", null, "Карпати"),
            new AuditChange("targets", null, "Весь курінь, Соколи, Олесь Юнак"));
    }

    [Fact]
    public async Task Handle_WhenAllTargetsAllowed_CreatesAndReturnsCreated()
    {
        var kurinKey = Guid.NewGuid();
        var target = new AgendaTargetInput { TargetType = AgendaTargetType.Group, TargetKey = Guid.NewGuid() };
        _currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());
        _access.Setup(a => a.AuthorizeTargetAsync(It.IsAny<AgendaTargetInput>(), ResourceAction.Create, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());

        var result = await _handler.Handle(CommandWith(kurinKey, target), default);

        result.Type.Should().Be(ResultType.Created);
        result.Data.Should().NotBe(Guid.Empty);
        _agendaRepo.Verify(r => r.Create(It.Is<AgendaItem>(a => a.KurinKey == kurinKey && a.Assignments.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
