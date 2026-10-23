using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Archive;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

public class SetAgendaArchivePolicyHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly JournalRecorder _journal = new(Mock.Of<ICurrentUserContext>());
    private readonly Kurin _kurin = new(5) { TaskAutoArchiveAfterDays = 14, TaskArchivePurgeAfterDays = null };
    private readonly SetAgendaArchivePolicyCommandHandler _handler;

    public SetAgendaArchivePolicyHandlerTests()
    {
        _uow.SetupGet(u => u.Kurins).Returns(_kurins.Object);
        _kurins.Setup(r => r.GetByKeyAsync(_kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(_kurin);
        _handler = new SetAgendaArchivePolicyCommandHandler(_uow.Object, _journal.Audit);
    }

    [Fact]
    public async Task AChangedPeriod_IsRecordedUnderTheKurin()
    {
        var result = await _handler.Handle(new SetAgendaArchivePolicyCommand(_kurin.KurinKey, 30, 365), default);

        result.Type.Should().Be(ResultType.Success);
        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Area.Should().Be(AuditArea.Settings);
        entry.Action.Should().Be(AuditActions.Settings.ArchivePolicyChanged);
        entry.KurinKey.Should().Be(_kurin.KurinKey);
        entry.SubjectLabel.Should().Be(SetAgendaArchivePolicyCommandHandler.Label);
        entry.Changes.Should().Equal(
            new AuditChange("autoArchiveAfterDays", "14", "30"),
            new AuditChange("purgeAfterDays", null, "365"));
    }

    [Fact]
    public async Task TheSamePeriods_RecordNothing()
    {
        await _handler.Handle(new SetAgendaArchivePolicyCommand(_kurin.KurinKey, 14, null), default);

        _journal.Entries.Should().BeEmpty();
    }
}
