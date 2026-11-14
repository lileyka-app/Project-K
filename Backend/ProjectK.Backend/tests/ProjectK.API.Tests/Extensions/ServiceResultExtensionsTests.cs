using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Extensions;
using ProjectK.API.Tests.TestHelpers;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.Extensions;

public class ServiceResultExtensionsTests
{
    private readonly ControllerBase _controller = new TestController
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public void AUnitSuccess_AnswersLikeAnEmptyObjectResult()
    {
        var unit = new ServiceResult<Unit>(ResultType.Success, Unit.Value).ToActionResult(_controller);
        var empty = new ServiceResult<object>(ResultType.Success).ToActionResult(_controller);

        var unitOk = Assert.IsType<OkObjectResult>(unit);
        var emptyOk = Assert.IsType<OkObjectResult>(empty);
        Assert.Null(unitOk.Value);
        Assert.Equal(emptyOk.Value, unitOk.Value);
    }

    [Fact]
    public void AUnitFailureWithoutACode_StillGetsTheDefaultError()
    {
        var result = new ServiceResult<Unit>(ResultType.NotFound).ToActionResult(_controller);

        ApiErrorAssert.HasError(result, StatusCodes.Status404NotFound, "NotFound");
    }

    private sealed class TestController : ControllerBase;
}
