using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;
using ProjectK.Infrastructure.Services.BlobStorageService;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ProjectK.Infrastructure.Tests.Services.BlobStorageService;

public class AzureBlobPhotoServiceTests
{
    private readonly AzureBlobPhotoService _service;
    private readonly Mock<ILogger<AzureBlobPhotoService>> _loggerMock;

    public AzureBlobPhotoServiceTests()
    {
        _loggerMock = new Mock<ILogger<AzureBlobPhotoService>>();

        // Provide a dummy connection string to satisfy the constructor
        var options = new BlobStorageOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=dummy;AccountKey=dummykey;EndpointSuffix=core.windows.net",
            ContainerName = "test-photos"
        };

        _service = new AzureBlobPhotoService(options, Mock.Of<IBlobReadLinks>(), _loggerMock.Object);
    }

    [Fact]
    public async Task CompressImageAsync_ShouldResizeAndCompressLargeImage()
    {
        // Arrange
        // Generate a large 3000x3000 image
        using var rawImage = new Image<Rgba32>(3000, 3000);

        // Fill with some data to ensure it has a measurable size
        var rnd = new Random(42);
        rawImage.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    pixelRow[x] = new Rgba32((byte)rnd.Next(255), (byte)rnd.Next(255), (byte)rnd.Next(255));
                }
            }
        });

        using var msOriginal = new MemoryStream();
        await rawImage.SaveAsJpegAsync(msOriginal, new JpegEncoder { Quality = 100 });
        byte[] originalBytes = msOriginal.ToArray();

        // Act
        var result = await _service.CompressImageAsync(originalBytes, "test.png", CancellationToken.None);

        // Assert
        Assert.Equal(".jpg", result.FinalExtension);
        Assert.True(result.ProcessedBytes.Length < originalBytes.Length, "The compressed image should be smaller than the original uncompressed image.");

        // Verify the dimensions were resized to max 1920x1920
        using var processedImage = Image.Load(result.ProcessedBytes);
        Assert.True(processedImage.Width <= 1920);
        Assert.True(processedImage.Height <= 1920);
    }

    [Fact]
    public async Task CompressImageAsync_ShouldRefuse_WhenFileIsNotAnImage()
    {
        // A file that does not decode used to be stored as it came, under its own extension and
        // content type — while the container was public, the storage domain served whatever was uploaded.
        byte[] invalidImageBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 }; // Invalid image header
        string fileName = "document.pdf";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CompressImageAsync(invalidImageBytes, fileName, CancellationToken.None));
    }

    [Fact]
    public async Task CompressImageAsync_ShouldRefuse_ATiffSentAsAPhoto()
    {
        // Only PNG, JPEG and WebP are decoded: the TIFF decoder is where ImageSharp 3.x advisories sit,
        // and the declared content type is no guard, since the format is told by the bytes.
        using var image = new Image<Rgba32>(4, 4);
        using var tiff = new MemoryStream();
        await image.SaveAsync(tiff, new SixLabors.ImageSharp.Formats.Tiff.TiffEncoder());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CompressImageAsync(tiff.ToArray(), "photo.png", CancellationToken.None));
    }

    [Fact]
    public async Task CompressImageAsync_ShouldMaintainSizeIfAlreadySmall()
    {
        // Arrange
        // Generate a small 100x100 image
        using var rawImage = new Image<Rgba32>(100, 100);
        using var msOriginal = new MemoryStream();
        await rawImage.SaveAsJpegAsync(msOriginal, new JpegEncoder { Quality = 75 });
        byte[] originalBytes = msOriginal.ToArray();

        // Act
        var result = await _service.CompressImageAsync(originalBytes, "small.jpg", CancellationToken.None);

        // Assert
        Assert.Equal(".jpg", result.FinalExtension);

        // Verify dimensions remained the same
        using var processedImage = Image.Load(result.ProcessedBytes);
        Assert.Equal(100, processedImage.Width);
        Assert.Equal(100, processedImage.Height);
    }

    [Fact]
    public async Task PrepareUploadAsync_ShouldKeepMemberPhotosAsJpeg()
    {
        // Arrange
        using var rawImage = new Image<Rgba32>(120, 80);
        using var msOriginal = new MemoryStream();
        await rawImage.SaveAsPngAsync(msOriginal, new PngEncoder());
        var originalBytes = msOriginal.ToArray();

        // Act
        var result = await _service.PrepareUploadAsync(
            originalBytes,
            "avatar.png",
            BlobUploadContext.MemberPhoto,
            CancellationToken.None);

        // Assert
        Assert.Equal(".jpg", result.FinalExtension);
        Assert.Equal("image/jpeg", result.ContentType);
        using var processedImage = Image.Load(result.ProcessedBytes);
        Assert.Equal(120, processedImage.Width);
        Assert.Equal(80, processedImage.Height);
    }

    [Fact]
    public async Task PrepareUploadAsync_ShouldEncodeGroupSilhouetteAsPng()
    {
        // Arrange
        using var rawImage = new Image<Rgba32>(64, 64);
        rawImage[0, 0] = new Rgba32(255, 0, 0, 0);
        using var msOriginal = new MemoryStream();
        await rawImage.SaveAsJpegAsync(msOriginal, new JpegEncoder { Quality = 90 });
        var originalBytes = msOriginal.ToArray();

        // Act
        var result = await _service.PrepareUploadAsync(
            originalBytes,
            "silhouette.jpg",
            BlobUploadContext.GroupSilhouette,
            CancellationToken.None);

        // Assert
        Assert.Equal(".png", result.FinalExtension);
        Assert.Equal("image/png", result.ContentType);
        using var processedImage = Image.Load(result.ProcessedBytes);
        Assert.Equal(64, processedImage.Width);
        Assert.Equal(64, processedImage.Height);
    }

    [Fact]
    public void BuildBlobName_ShouldUseScenarioFolderAndDate()
    {
        // Arrange
        var utcNow = new DateTime(2026, 5, 27, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var memberBlobName = AzureBlobPhotoService.BuildBlobName(BlobUploadContext.MemberPhoto, ".jpg", utcNow);
        var silhouetteBlobName = AzureBlobPhotoService.BuildBlobName(BlobUploadContext.GroupSilhouette, ".png", utcNow);

        // Assert
        Assert.Matches(@"^member-photos/2026/05/27/[a-f0-9]{32}\.jpg$", memberBlobName);
        Assert.Matches(@"^group-silhouettes/2026/05/27/[a-f0-9]{32}\.png$", silhouetteBlobName);
    }

    [Fact]
    public async Task PrepareUploadAsync_ShouldRejectInvalidGroupSilhouetteImage()
    {
        // Arrange
        byte[] invalidImageBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PrepareUploadAsync(
                invalidImageBytes,
                "broken.webp",
                BlobUploadContext.GroupSilhouette,
                CancellationToken.None));
    }
}
