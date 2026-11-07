using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Emblem;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.KurinHandlers;

public class KurinEmblemHandlerTests
{
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPhotoService> _photos = new();
    private readonly IMapper _mapper;

    public KurinEmblemHandlerTests()
    {
        _unitOfWork.Setup(u => u.Kurins).Returns(_kurins.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _photos.Setup(p => p.UploadPhotoAsync(It.IsAny<byte[]>(), It.IsAny<string>(), BlobUploadContext.KurinEmblem, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoUploadResult("kurin-emblems/new.png", "image/png"));
        _mapper = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(FakeBlobReadLinks.Resolvers);
            cfg.AddProfile(new KurinModuleProfile());
        }, LoggerFactory.Create(_ => { })).CreateMapper();
    }

    private UploadKurinEmblemCommandHandler Uploader() => new(_unitOfWork.Object, _photos.Object, _mapper, Mock.Of<IBackendCache>());

    private Kurin Given(Kurin kurin)
    {
        _kurins.Setup(r => r.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(kurin);
        return kurin;
    }

    [Fact]
    public async Task Upload_ShouldRefuse_AYouthKurin_WhichKeepsItsPlate()
    {
        var youth = Given(new Kurin(47));

        var result = await Uploader().Handle(new UploadKurinEmblemCommand(youth.KurinKey, [1, 2, 3], "e.png"), default);

        result.ErrorCode.Should().Be(UploadKurinEmblemCommandHandler.NotSeniorCode);
        youth.EmblemBlobName.Should().BeNull();
        _photos.Verify(p => p.UploadPhotoAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<BlobUploadContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_ShouldReplaceASeniorKurinsEmblem_AndDropTheOldPicture()
    {
        var senior = Given(new Kurin(5, KurinBranch.USP) { EmblemBlobName = "kurin-emblems/old.png" });

        var result = await Uploader().Handle(new UploadKurinEmblemCommand(senior.KurinKey, [1, 2, 3], "e.png"), default);

        result.Type.Should().Be(ResultType.Success);
        senior.EmblemBlobName.Should().Be("kurin-emblems/new.png");
        result.Data!.EmblemUrl.Should().NotBeNullOrEmpty();
        _photos.Verify(p => p.DeletePhotoAsync("kurin-emblems/old.png", It.IsAny<CancellationToken>()), Times.Once);
    }
}
