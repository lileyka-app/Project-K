using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.Behaviors;

public class RequestTimingBehaviorTests
{
    public sealed record SampleRequest : IRequest<ServiceResult<bool>>;

    [Fact]
    public async Task Handle_ReturnsHandlerResponse_AndRunsHandlerOnce()
    {
        var behavior = new RequestTimingBehavior<SampleRequest, ServiceResult<bool>>(
            NullLogger<RequestTimingBehavior<SampleRequest, ServiceResult<bool>>>.Instance);
        var expected = new ServiceResult<bool>(ResultType.Success, true);
        var calls = 0;

        var result = await behavior.Handle(
            new SampleRequest(),
            _ => { calls++; return Task.FromResult(expected); },
            CancellationToken.None);

        result.Should().BeSameAs(expected);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PropagatesException()
    {
        var behavior = new RequestTimingBehavior<SampleRequest, ServiceResult<bool>>(
            NullLogger<RequestTimingBehavior<SampleRequest, ServiceResult<bool>>>.Instance);

        var act = async () => await behavior.Handle(
            new SampleRequest(),
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
