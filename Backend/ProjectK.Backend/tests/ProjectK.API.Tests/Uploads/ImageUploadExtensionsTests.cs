using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Tests.TestHelpers;
using ProjectK.API.Uploads;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.Uploads;

public class ImageUploadExtensionsTests
{
    private readonly ControllerBase _controller = new TestController
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task SendImage_ShouldRefuseAMissingFile_WithMissingImage()
    {
        var sent = false;

        var result = await _controller.SendImageAsync(null, (_, _) => Send(ref sent), CancellationToken.None);

        ApiErrorAssert.HasError(result, StatusCodes.Status400BadRequest, "MissingImage");
        Assert.False(sent);
    }

    [Fact]
    public async Task SendImage_ShouldRefuseAWrongType_BeforeReadingIt()
    {
        var sent = false;

        var result = await _controller.SendImageAsync(File("page.html", "text/html", 10), (_, _) => Send(ref sent), CancellationToken.None);

        ApiErrorAssert.HasError(result, StatusCodes.Status400BadRequest, "UnsupportedImageType");
        Assert.False(sent);
    }

    [Fact]
    public async Task SendImage_ShouldRefuseAnImageOverTheLimit()
    {
        var sent = false;

        var result = await _controller.SendImageAsync(File("big.png", "image/png", 2048), (_, _) => Send(ref sent), CancellationToken.None, maxBytes: 1024);

        ApiErrorAssert.HasError(result, StatusCodes.Status400BadRequest, "ImageTooLarge");
        Assert.False(sent);
    }

    [Fact]
    public async Task SendImage_ShouldHandTheBytesAndNameOn()
    {
        byte[]? received = null;
        string? receivedName = null;

        var result = await _controller.SendImageAsync(
            File("emblem.png", "image/png", 16),
            (bytes, name) =>
            {
                received = bytes;
                receivedName = name;
                return Task.FromResult(new ServiceResult<string>(ResultType.Success, "stored"));
            },
            CancellationToken.None);

        Assert.Equal("stored", Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(16, received?.Length);
        Assert.Equal("emblem.png", receivedName);
    }

    [Fact]
    public void RefuseImage_ShouldLetAMissingOptionalImageThrough()
    {
        Assert.Null(_controller.RefuseImage(null));
    }

    [Fact]
    public void RefuseRequiredImage_ShouldAnswerWithTheEndpointsOwnCode()
    {
        var result = _controller.RefuseRequiredImage(null, "FileRequired", "Attach an image.");

        ApiErrorAssert.HasError(result!, StatusCodes.Status400BadRequest, "FileRequired");
    }

    private static Task<ServiceResult<string>> Send(ref bool sent)
    {
        sent = true;
        return Task.FromResult(new ServiceResult<string>(ResultType.Success, "stored"));
    }

    private static FormFile File(string name, string contentType, int length)
    {
        var bytes = Encoding.ASCII.GetBytes(new string('x', length));
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class TestController : ControllerBase;
}
