using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProjectK.API.Controllers.KurinModule;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.KurinModule.ControllerTests;

/// <summary>
/// The two ways into the box: <c>?include=</c> names the folders to open, and
/// <c>/dossier/{folder}</c> hands back one folder's contents on its own.
/// </summary>
public class MemberDossierControllerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly MemberController _controller;
    private GetMemberDossierQuery? _sent;

    public MemberDossierControllerTests()
    {
        _controller = new MemberController(_mediator.Object);
        _mediator
            .Setup(m => m.Send(It.IsAny<GetMemberDossierQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => _sent = (GetMemberDossierQuery)request)
            .ReturnsAsync(() => new ServiceResult<MemberDossierDto>(ResultType.Success, Box()));
    }

    private static MemberDossierDto Box() => new(
        Profile: new MemberDossierProfileDto(
            Guid.NewGuid(), "PL-ABCDE-12345", null, "Оксана", null, "Тестова",
            null, null, null, MemberProfileVerificationStatus.Unverified, null),
        Folders: [new(MemberDossierFolders.Warnings, 1)],
        Memberships: [],
        Offices: null,
        Levels: null,
        Awards: null,
        Warnings: [new MemberWarningDto()],
        Progress: null);

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("warnings", 1)]
    [InlineData("warnings, levels", 2)]
    [InlineData("all", 6)]
    public async Task Include_ShouldBeTakenApartBeforeItReachesTheUseCase(string? include, int expected)
    {
        await _controller.GetDossier(Guid.NewGuid(), include);

        Assert.NotNull(_sent);
        Assert.Equal(expected, _sent!.Include.Count);
    }

    [Fact]
    public async Task AFolderRoute_ShouldAskForThatFolderAndReturnOnlyIt()
    {
        var result = await _controller.GetDossierFolder(Guid.NewGuid(), MemberDossierFolders.Warnings);

        Assert.Equal([MemberDossierFolders.Warnings], _sent!.Include);
        var ok = Assert.IsType<OkObjectResult>(result);
        var warnings = Assert.IsAssignableFrom<IReadOnlyCollection<MemberWarningDto>>(ok.Value);
        Assert.Single(warnings);
    }

    [Fact]
    public async Task AFolderRoute_ShouldPassOnARefusal_RatherThanAnEmptyFolder()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetMemberDossierQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<MemberDossierDto>.Failure(
                ResultType.BadRequest, "UnknownDossierFolder", "No such folder: salary."));

        var result = await _controller.GetDossierFolder(Guid.NewGuid(), "salary");

        Assert.IsNotType<OkObjectResult>(result);
    }
}
