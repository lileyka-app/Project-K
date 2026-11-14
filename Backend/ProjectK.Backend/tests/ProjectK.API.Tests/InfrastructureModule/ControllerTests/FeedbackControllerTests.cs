using System.Text;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProjectK.API.Controllers.InfrastructureModule;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Feedback;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Dtos.InfrastructureModule.Requests;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.InfrastructureModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.API.Tests.InfrastructureModule.ControllerTests;

public class FeedbackControllerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly FeedbackController _controller;

    public FeedbackControllerTests()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.UserAgent = "Mozilla/5.0 Test";
        _controller = new FeedbackController(_mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    // The user agent comes from the request, so the body cannot claim a browser it is not.
    [Fact]
    public async Task ReportProblem_ShouldPassTheRequestUserAgent_AndReturnTheReceipt()
    {
        ReportProblemCommand? sent = null;
        _mediator
            .Setup(m => m.Send(It.IsAny<ReportProblemCommand>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<ServiceResult<ProblemReportReceipt>> c, CancellationToken _) => sent = (ReportProblemCommand)c)
            .ReturnsAsync(new ServiceResult<ProblemReportReceipt>(ResultType.Success, new ProblemReportReceipt("https://github.com/x/y/issues/3", 3)));

        var result = await _controller.ReportProblem(
            new ReportProblemRequest("T", "D", null, null, "/kurin", "v1", null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(3, Assert.IsType<ProblemReportReceipt>(ok.Value).IssueNumber);
        Assert.Equal("Mozilla/5.0 Test", sent!.UserAgent);
        Assert.Equal("/kurin", sent.Route);
    }

    [Fact]
    public async Task UploadScreenshot_ShouldRefuseNothing_AndNonImages_BeforeTouchingStorage()
    {
        var empty = await _controller.UploadScreenshot(null, CancellationToken.None);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(empty).StatusCode);

        var html = FormFile("page.html", "text/html", "<html>");
        var refused = await _controller.UploadScreenshot(html, CancellationToken.None);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(refused).StatusCode);

        _mediator.Verify(m => m.Send(It.IsAny<UploadFeedbackScreenshotCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadScreenshot_ShouldAnswerWithTheUrl()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<UploadFeedbackScreenshotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<string>(ResultType.Success, "https://blob/feedback-screenshots/a.png"));

        var result = await _controller.UploadScreenshot(FormFile("shot.png", "image/png", "png-bytes"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("https://blob/feedback-screenshots/a.png", Assert.IsType<ScreenshotUploadedDto>(ok.Value).Url);
    }

    private static FormFile FormFile(string name, string contentType, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
