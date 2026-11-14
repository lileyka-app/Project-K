using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProjectK.API.Controllers.ScoreModule;
using ProjectK.API.Tests.TestHelpers;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.ScoreModule;

public class KurinScoreControllerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly KurinScoreController _controller;

    public KurinScoreControllerTests()
    {
        _controller = new KurinScoreController(_mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Theory]
    [InlineData("yesterday")]
    [InlineData("")]
    public async Task AnOccurrenceThatIsNotAnInstant_ShouldBeRefusedInTheStandardShape(string occurrence)
    {
        var result = await _controller.SetNotHeld(Guid.NewGuid(), Guid.NewGuid(), occurrence);

        ApiErrorAssert.HasError(result, StatusCodes.Status400BadRequest, "BadOccurrence");
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnOccurrence_ShouldReachTheHandlerAsItsUtcStart()
    {
        var kurinKey = Guid.NewGuid();
        var agendaItemKey = Guid.NewGuid();
        _mediator
            .Setup(m => m.Send(It.IsAny<SetOccurrenceNotHeldCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<object>(ResultType.Success, new object()));

        await _controller.ClearNotHeld(kurinKey, agendaItemKey, "2026-10-06T16:00:00Z");

        _mediator.Verify(m => m.Send(
            new SetOccurrenceNotHeldCommand(kurinKey, agendaItemKey, new DateTime(2026, 10, 6, 16, 0, 0, DateTimeKind.Utc), false),
            It.IsAny<CancellationToken>()));
    }
}
