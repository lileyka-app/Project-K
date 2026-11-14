using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession;
using ProjectK.Common.Entities.KurinModule.Planning;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.PlanningHandlers;

public class PlanningSessionHandlerTests
{
    private readonly Mock<IPlanningSessionRepository> _sessions = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Guid _author = Guid.NewGuid();

    public PlanningSessionHandlerTests()
    {
        _unitOfWork.Setup(u => u.PlanningSessions).Returns(_sessions.Object);
        _currentUser.Setup(c => c.Roles).Returns(Array.Empty<string>());
    }

    [Fact]
    public async Task GetByKey_ShouldAnswerTheSessionWithItsKeysAsText_AndLetItsAuthorDeleteIt()
    {
        var session = Session();
        _sessions.Setup(r => r.GetByKeyWithDetailsAsync(session.PlanningSessionKey, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _currentUser.Setup(c => c.UserId).Returns(_author);

        var result = await new GetPlanningSessionByKeyQueryHandler(_unitOfWork.Object, _currentUser.Object)
            .Handle(new GetPlanningSessionByKeyQuery(session.PlanningSessionKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().BeEquivalentTo(new
        {
            session.PlanningSessionKey,
            session.Name,
            KurinKey = session.KurinKey.ToString(),
            session.DurationDays,
            session.OptimalStartDate,
            session.ConflictScore,
            IsCalculated = true,
            CanDelete = true
        });
        var participant = session.Participants.Single();
        result.Data!.Participants.Should().ContainSingle().Which.Should().BeEquivalentTo(new PlanningParticipantDto(
            participant.MemberKey.ToString(),
            participant.FullName,
            participant.RoleWeight,
            [new DateRangeDto(new DateTime(2026, 7, 2), new DateTime(2026, 7, 4))]));
    }

    [Fact]
    public async Task GetAll_ShouldNotLetSomeoneElseDeleteASession()
    {
        var session = Session();
        _sessions.Setup(r => r.GetAllByKurinKeyAsync(session.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([session]);
        _currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());

        var result = await new GetPlanningSessionsQueryHandler(_unitOfWork.Object, _currentUser.Object)
            .Handle(new GetPlanningSessionsQuery(session.KurinKey), CancellationToken.None);

        result.Data.Should().ContainSingle().Which.CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task GetByKey_WhenThereIsNoSuchSession_ShouldSaySoByCode()
    {
        var result = await new GetPlanningSessionByKeyQueryHandler(_unitOfWork.Object, _currentUser.Object)
            .Handle(new GetPlanningSessionByKeyQuery(Guid.NewGuid()), CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        result.ErrorCode.Should().Be(KurinErrors.PlanningSessionNotFound);
    }

    [Fact]
    public async Task Delete_WhenThereIsNoSuchSession_ShouldSaySoByCode()
    {
        var result = await new DeletePlanningSessionCommandHandler(_unitOfWork.Object)
            .Handle(new DeletePlanningSessionCommand(Guid.NewGuid()), CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        result.ErrorCode.Should().Be("PlanningSessionNotFound");
    }

    private PlanningSession Session() => new()
    {
        PlanningSessionKey = Guid.NewGuid(),
        KurinKey = Guid.NewGuid(),
        Name = "Табір",
        CreatedByUserKey = _author,
        SearchStart = new DateTime(2026, 7, 1),
        SearchEnd = new DateTime(2026, 7, 31),
        DurationDays = 10,
        IsCalculated = true,
        OptimalStartDate = new DateTime(2026, 7, 10),
        OptimalEndDate = new DateTime(2026, 7, 20),
        ConflictScore = 1.5,
        Participants =
        [
            new PlanningParticipant
            {
                MemberKey = Guid.NewGuid(),
                FullName = "Іван Петренко",
                RoleWeight = 2,
                BusyRanges = [new ParticipantBusyRange { Start = new DateTime(2026, 7, 2), End = new DateTime(2026, 7, 4) }]
            }
        ]
    };
}
