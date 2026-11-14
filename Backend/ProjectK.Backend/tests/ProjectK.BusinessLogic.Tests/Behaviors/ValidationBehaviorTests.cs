using FluentAssertions;
using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.Behaviors;

public class ValidationBehaviorTests
{
    public sealed record SampleCommand(string Name) : IRequest<ServiceResult<bool>>;

    public sealed class SampleValidator : AbstractValidator<SampleCommand>
    {
        public SampleValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }

    private static readonly ServiceResult<bool> HandlerResult = new(ResultType.Success, true);

    [Fact]
    public async Task Handle_PassesThrough_WhenNoValidatorsRegistered()
    {
        var behavior = new ValidationBehavior<SampleCommand, ServiceResult<bool>>([]);

        var result = await behavior.Handle(new SampleCommand("ok"), _ => Task.FromResult(HandlerResult), CancellationToken.None);

        result.Should().BeSameAs(HandlerResult);
    }

    [Fact]
    public async Task Handle_RunsHandler_WhenValidationPasses()
    {
        var behavior = new ValidationBehavior<SampleCommand, ServiceResult<bool>>([new SampleValidator()]);

        var result = await behavior.Handle(new SampleCommand("ok"), _ => Task.FromResult(HandlerResult), CancellationToken.None);

        result.Should().BeSameAs(HandlerResult);
    }

    [Fact]
    public async Task Handle_ReturnsBadRequest_WithoutRunningHandler_WhenValidationFails()
    {
        var behavior = new ValidationBehavior<SampleCommand, ServiceResult<bool>>([new SampleValidator()]);
        var handlerRan = false;

        var result = await behavior.Handle(
            new SampleCommand(string.Empty),
            _ => { handlerRan = true; return Task.FromResult(HandlerResult); },
            CancellationToken.None);

        handlerRan.Should().BeFalse();
        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("VALIDATION_ERROR");
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}
