using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Services.Audit;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule.HandlerTests;

/// <summary>
/// The third probe: five projects and a spare, at most two of them shared, pictures only from the
/// project itself, files that go when their row goes, and confirmations under the signing rights.
/// </summary>
public class ProbeProjectHandlerTests
{
    private const string ProbeId = "probe-3";

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IProbeProjectRepository> _projects = new();
    private readonly Mock<IBadgeProgressRepository> _badges = new();
    private readonly Mock<IProbeProjectAccess> _access = new();
    private readonly Mock<IBlobReadLinks> _links = new();
    private readonly Mock<IPhotoService> _photos = new();
    private readonly Mock<IProbesCatalogService> _probes = new();
    private readonly Mock<IThirdProbeReader> _reader = new();
    private readonly Mock<ICurrentUserContext> _user = new();
    private readonly JournalRecorder _journal;
    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly List<ProbeProject> _stored = [];
    private ThirdProbeFile? _file;

    public ProbeProjectHandlerTests()
    {
        _unitOfWork.SetupGet(u => u.ProbeProjects).Returns(_projects.Object);
        _unitOfWork.SetupGet(u => u.BadgeProgresses).Returns(_badges.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _access.Setup(a => a.IsProjectProbeAsync(_memberKey, ProbeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _access.Setup(a => a.CanEditAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _access.Setup(a => a.CanSignAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _access.Setup(a => a.CanUnsignAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _links.Setup(l => l.For(It.IsAny<string?>())).Returns<string?>(name => $"https://blob/{name}?sig");
        _user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        _user.SetupGet(u => u.KurinKey).Returns(Guid.NewGuid());
        _journal = new JournalRecorder(_user.Object, new AuditActorResolver(_user.Object, new Mock<IMemberDirectory>().Object));
        _reader.Setup(r => r.ReadAsync(_memberKey, ProbeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThirdProbeResponse(true, true, true, true, null, [], [], null!));
        _projects.Setup(r => r.GetForMemberProbeAsync(_memberKey, ProbeId, It.IsAny<CancellationToken>())).ReturnsAsync(() => _stored.ToList());
        _projects.Setup(r => r.Create(It.IsAny<ProbeProject>(), It.IsAny<CancellationToken>())).Callback<ProbeProject, CancellationToken>((p, _) => _stored.Add(p));
        _projects.Setup(r => r.GetWithImagesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid key, CancellationToken _) => _stored.FirstOrDefault(p => p.ProbeProjectKey == key));
        _projects.Setup(r => r.GetFileAsync(_memberKey, ProbeId, It.IsAny<CancellationToken>())).ReturnsAsync(() => _file);
        _projects.Setup(r => r.AddFile(It.IsAny<ThirdProbeFile>())).Callback<ThirdProbeFile>(f => _file = f);
    }

    private SaveProbeProjectCommandHandler SaveHandler() => new(
        _unitOfWork.Object, _access.Object, _photos.Object, _journal.Actors, _journal.Audit, _probes.Object, _reader.Object);

    private UploadProbeProjectImageCommandHandler UploadHandler() => new(_unitOfWork.Object, _access.Object, _photos.Object, _links.Object);

    private SignThirdProbeCommandHandler SignHandler() => new(
        _unitOfWork.Object, _access.Object, _journal.Actors, _journal.Audit, _probes.Object, _reader.Object);

    private SaveThirdProbeFileCommandHandler FileHandler() => new(
        _unitOfWork.Object, _access.Object, _journal.Actors, _journal.Audit, _probes.Object, _reader.Object);

    private UploadCampCertificateCommandHandler CertificateHandler() => new(
        _unitOfWork.Object, _access.Object, _photos.Object, _journal.Audit, _probes.Object, _reader.Object);

    private ProbeProject Given(bool isSpare = false, bool isShared = false, int images = 0, string title = "Похід")
    {
        var project = new ProbeProject { MemberKey = _memberKey, ProbeId = ProbeId, Title = title, IsSpare = isSpare, IsShared = isShared };
        for (var i = 0; i < images; i++)
        {
            project.Images.Add(new ProbeProjectImage { ProbeProjectKey = project.ProbeProjectKey, BlobName = $"probe-project-images/{i}.jpg" });
        }
        _stored.Add(project);
        return project;
    }

    private static ProbeProjectInput Input(string goal = "", string plan = "", string results = "", bool shared = false, string title = "Похід") =>
        new(title, ProbeProjectArea.Nature, [ProbeProjectReach.GroupOrKurin, ProbeProjectReach.Stanytsia], shared, shared ? "Олег, Марта" : null,
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 3), "Чорногора", "Провід куреня", "Олена Коваль", "+380501112233",
            "Що це за проєкт і моя роль", goal, plan, results);

    private SaveProbeProjectCommand Save(Guid? key, ProbeProjectInput input) => new(_memberKey, ProbeId, key, input);

    [Fact]
    public async Task Save_ShouldAddAProject_AsAMainOne_AndJournalIt()
    {
        var result = await SaveHandler().Handle(Save(null, Input(goal: "**Мета**")), CancellationToken.None);

        result.Type.Should().Be(ResultType.Created);
        var project = _stored.Single();
        project.IsSpare.Should().BeFalse();
        project.Reach.Should().Be(ProbeProjectReach.GroupOrKurin | ProbeProjectReach.Stanytsia);
        project.CoordinatorName.Should().Be("Олена Коваль");
        result.Data!.ProjectKey.Should().Be(project.ProbeProjectKey);
        _journal.Entries.Should().ContainSingle(e => e.Action == AuditActions.Probe.ProjectAdded);
    }

    [Fact]
    public async Task Save_ShouldMakeTheSixthTheSpare_AndRefuseTheSeventh()
    {
        for (var i = 0; i < ProbeProjectRules.MaxMainProjects; i++)
        {
            Given();
        }

        (await SaveHandler().Handle(Save(null, Input()), CancellationToken.None)).Type.Should().Be(ResultType.Created);
        _stored.Last().IsSpare.Should().BeTrue();

        var seventh = await SaveHandler().Handle(Save(null, Input()), CancellationToken.None);
        seventh.ErrorCode.Should().Be("TooManyProjects");
    }

    [Fact]
    public async Task Save_ShouldRefuseAThirdSharedMainProject_ButLetTheSpareBeShared()
    {
        Given(isShared: true);
        Given(isShared: true);
        var third = Given();
        var spare = Given(isSpare: true);

        (await SaveHandler().Handle(Save(third.ProbeProjectKey, Input(shared: true)), CancellationToken.None))
            .ErrorCode.Should().Be("TooManyShared");
        (await SaveHandler().Handle(Save(spare.ProbeProjectKey, Input(shared: true)), CancellationToken.None))
            .Type.Should().Be(ResultType.Success);
    }

    [Fact]
    public async Task Save_ShouldRefuseATextOverTheLimit()
    {
        var result = await SaveHandler().Handle(Save(null, Input(plan: new string('а', ProbeProjectRules.MaxTextLength + 1))), CancellationToken.None);

        result.ErrorCode.Should().Be("DescriptionTooLong");
    }

    [Theory]
    [InlineData("![x](https://example.com/cat.png)")]
    [InlineData("![x](data:image/png;base64,AAAA)")]
    public async Task Save_ShouldRefuseAPictureFromOutside_InAnyOfTheTexts(string markdown)
    {
        var project = Given();

        var result = await SaveHandler().Handle(Save(project.ProbeProjectKey, Input(results: markdown)), CancellationToken.None);

        result.ErrorCode.Should().Be("ForeignImage");
    }

    [Fact]
    public async Task Save_ShouldRefuseAPictureOfAnotherProject()
    {
        var project = Given();

        var result = await SaveHandler().Handle(Save(project.ProbeProjectKey, Input(goal: $"![фото](project-image:{Guid.NewGuid()})")), CancellationToken.None);

        result.ErrorCode.Should().Be("ForeignImage");
    }

    [Fact]
    public async Task Save_ShouldDropPicturesNoTextShowsAnyMore_RowsAndBlobs()
    {
        var project = Given(images: 2);
        var kept = project.Images.First();
        var dropped = project.Images.Last();

        var result = await SaveHandler().Handle(
            Save(project.ProbeProjectKey, Input(results: $"Ось:\n\n![фото](project-image:{kept.ProbeProjectImageKey})")), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        project.Images.Should().ContainSingle().Which.Should().Be(kept);
        _projects.Verify(r => r.RemoveImages(It.Is<IEnumerable<ProbeProjectImage>>(list => list.Single() == dropped)), Times.Once);
        _photos.Verify(p => p.DeletePhotoAsync(dropped.BlobName, It.IsAny<CancellationToken>()), Times.Once);
        _photos.Verify(p => p.DeletePhotoAsync(kept.BlobName, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Save_ShouldBeForbidden_ToSomeoneWhoMayOnlyRead()
    {
        _access.Setup(a => a.CanEditAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        (await SaveHandler().Handle(Save(null, Input()), CancellationToken.None)).Type.Should().Be(ResultType.Forbidden);
    }

    [Fact]
    public async Task Save_ShouldRefuse_AProbeThatIsNotTheThird()
    {
        _access.Setup(a => a.IsProjectProbeAsync(_memberKey, ProbeId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        (await SaveHandler().Handle(Save(null, Input()), CancellationToken.None)).ErrorCode.Should().Be("NotProjectProbe");
    }

    [Fact]
    public async Task Upload_ShouldRefuseAFileOverOneMegabyte_BeforeStoringIt()
    {
        var project = Given();

        var result = await UploadHandler().Handle(
            new UploadProbeProjectImageCommand(_memberKey, ProbeId, project.ProbeProjectKey, new byte[ProbeProjectRules.MaxImageBytes + 1], "big.png"),
            CancellationToken.None);

        result.ErrorCode.Should().Be("ImageTooLarge");
        _photos.Verify(p => p.UploadPhotoAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<BlobUploadContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_ShouldRefuseTheSixthPicture()
    {
        var project = Given(images: ProbeProjectRules.MaxImagesPerProject);

        var result = await UploadHandler().Handle(
            new UploadProbeProjectImageCommand(_memberKey, ProbeId, project.ProbeProjectKey, [1, 2, 3], "a.png"), CancellationToken.None);

        result.ErrorCode.Should().Be("TooManyImages");
    }

    [Fact]
    public async Task Upload_ShouldStoreThePicture_InTheProjectFolder()
    {
        var project = Given();
        _photos.Setup(p => p.UploadPhotoAsync(It.IsAny<byte[]>(), "a.png", BlobUploadContext.ProbeProjectImage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoUploadResult("probe-project-images/new.jpg", "ignored", 3));

        var result = await UploadHandler().Handle(
            new UploadProbeProjectImageCommand(_memberKey, ProbeId, project.ProbeProjectKey, [1, 2, 3], "a.png"), CancellationToken.None);

        result.Data!.Url.Should().Be("https://blob/probe-project-images/new.jpg?sig");
        _projects.Verify(r => r.AddImage(It.Is<ProbeProjectImage>(i => i.BlobName == "probe-project-images/new.jpg")), Times.Once);
    }

    [Fact]
    public async Task Delete_ShouldRemoveTheProjectAndItsBlobs_AndLetTheSpareTakeItsPlace()
    {
        var project = Given(images: 2);
        var spare = Given(isSpare: true);
        var handler = new DeleteProbeProjectCommandHandler(_unitOfWork.Object, _access.Object, _photos.Object, _journal.Audit, _probes.Object, _reader.Object);

        var result = await handler.Handle(new DeleteProbeProjectCommand(_memberKey, ProbeId, project.ProbeProjectKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _projects.Verify(r => r.Delete(project, It.IsAny<CancellationToken>()), Times.Once);
        _photos.Verify(p => p.DeletePhotoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        spare.IsSpare.Should().BeFalse();
        _journal.Entries.Should().ContainSingle(e => e.Action == AuditActions.Probe.ProjectRemoved);
    }

    [Fact]
    public async Task Delete_ShouldKeepASharedSpare_WhenTwoSharedMainProjectsRemain()
    {
        Given(isShared: true);
        Given(isShared: true);
        var project = Given();
        var spare = Given(isSpare: true, isShared: true);
        var handler = new DeleteProbeProjectCommandHandler(_unitOfWork.Object, _access.Object, _photos.Object, _journal.Audit, _probes.Object, _reader.Object);

        var result = await handler.Handle(new DeleteProbeProjectCommand(_memberKey, ProbeId, project.ProbeProjectKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        spare.IsSpare.Should().BeTrue();
    }

    [Fact]
    public async Task Swap_ShouldExchangeTheSpareAndAMainProject()
    {
        var main = Given(title: "Шпаківні");
        var spare = Given(isSpare: true, title: "Ватра");
        var handler = new SwapSpareProjectCommandHandler(_unitOfWork.Object, _access.Object, _journal.Audit, _probes.Object, _reader.Object);

        var result = await handler.Handle(new SwapSpareProjectCommand(_memberKey, ProbeId, spare.ProbeProjectKey, main.ProbeProjectKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        (main.IsSpare, spare.IsSpare).Should().Be((true, false));
        _journal.Entries.Should().ContainSingle(e => e.Action == AuditActions.Probe.ProjectsSwapped);
    }

    [Fact]
    public async Task Swap_ShouldRefuse_WhenItWouldMakeAThirdSharedMainProject()
    {
        Given(isShared: true);
        Given(isShared: true);
        var main = Given();
        var spare = Given(isSpare: true, isShared: true);
        var handler = new SwapSpareProjectCommandHandler(_unitOfWork.Object, _access.Object, _journal.Audit, _probes.Object, _reader.Object);

        var result = await handler.Handle(new SwapSpareProjectCommand(_memberKey, ProbeId, spare.ProbeProjectKey, main.ProbeProjectKey), CancellationToken.None);

        result.ErrorCode.Should().Be("TooManyShared");
        spare.IsSpare.Should().BeTrue();
    }

    [Fact]
    public async Task Sign_ShouldMarkAProjectDone_WithTheCoordinatorsWords()
    {
        var project = Given();

        await SignHandler().Handle(
            new SignThirdProbeCommand(_memberKey, ProbeId, ThirdProbeSignPart.ProjectCompletion, true, project.ProbeProjectKey, "Взірцево"),
            CancellationToken.None);

        project.CompletedAtUtc.Should().NotBeNull();
        project.CoordinatorComment.Should().Be("Взірцево");
        _journal.Entries.Should().ContainSingle(e => e.Action == AuditActions.Probe.ProjectCompleted);
    }

    [Fact]
    public async Task Sign_ShouldUseTheUnsigningRight_ToTakeAConfirmationBack()
    {
        var project = Given();
        project.ApprovedAtUtc = DateTime.UtcNow;
        _access.Setup(a => a.CanUnsignAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await SignHandler().Handle(
            new SignThirdProbeCommand(_memberKey, ProbeId, ThirdProbeSignPart.ProjectApproval, false, project.ProbeProjectKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        project.ApprovedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Sign_ShouldNotConfirmAReflectionWithNoDate()
    {
        var result = await SignHandler().Handle(
            new SignThirdProbeCommand(_memberKey, ProbeId, ThirdProbeSignPart.Reflection, true), CancellationToken.None);

        result.ErrorCode.Should().Be("NothingToConfirm");
    }

    [Fact]
    public async Task File_ShouldDropTheReflectionConfirmation_WhenTheReflectionChanges()
    {
        _file = new ThirdProbeFile
        {
            MemberKey = _memberKey, ProbeId = ProbeId, ReflectionOn = new DateOnly(2026, 3, 1), ReflectionWith = "Звʼязковий",
            ReflectionConfirmedAtUtc = DateTime.UtcNow, ReflectionConfirmedByName = "Звʼязковий"
        };

        await FileHandler().Handle(
            new SaveThirdProbeFileCommand(_memberKey, ProbeId, FileInput(reflectionOn: new DateOnly(2026, 3, 8))), CancellationToken.None);

        _file.ReflectionConfirmedAtUtc.Should().BeNull();
        _file.ReflectionConfirmedByName.Should().BeNull();
    }

    [Fact]
    public async Task File_ShouldWriteTheMentorsByRole_AndTheCampWithoutAnyConfirmation()
    {
        await FileHandler().Handle(
            new SaveThirdProbeFileCommand(_memberKey, ProbeId,
                FileInput() with { MentorZvyazkovyi = " Володимир Іваненко ", MentorReferent = "Олена Коваль", CampKind = ThirdProbeCampKind.Kvt, CampName = "КВТ" }),
            CancellationToken.None);

        _file!.MentorZvyazkovyi.Should().Be("Володимир Іваненко");
        _file.MentorVporiadnyk.Should().BeNull();
        _file.MentorReferent.Should().Be("Олена Коваль");
        _file.CampName.Should().Be("КВТ");
    }

    [Fact]
    public async Task File_ShouldAcceptOnlyAHeldVmilistForTilovykhovannia()
    {
        _badges.Setup(b => b.GetByMemberAndBadgeIdAsync(_memberKey, "biker", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BadgeProgress { BadgeId = "biker", Status = BadgeProgressStatus.Submitted });

        var result = await FileHandler().Handle(
            new SaveThirdProbeFileCommand(_memberKey, ProbeId, FileInput() with { PhysicalBadgeId = "biker" }), CancellationToken.None);

        result.ErrorCode.Should().Be("BadgeNotHeld");
    }

    [Fact]
    public async Task File_ShouldKeepPicturesOutOfTheResume()
    {
        var result = await FileHandler().Handle(
            new SaveThirdProbeFileCommand(_memberKey, ProbeId, FileInput() with { Resume = $"![x](project-image:{Guid.NewGuid()})" }),
            CancellationToken.None);

        result.ErrorCode.Should().Be("ForeignImage");
    }

    [Fact]
    public async Task Upload_ShouldRefuse_WhenThePersonsStorageIsFull()
    {
        var project = Given();
        _projects.Setup(r => r.GetStoredBytesAsync(_memberKey, ProbeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProbeProjectRules.MaxStoredBytes);

        var result = await UploadHandler().Handle(
            new UploadProbeProjectImageCommand(_memberKey, ProbeId, project.ProbeProjectKey, new byte[100], "a.png"), CancellationToken.None);

        result.ErrorCode.Should().Be("StorageFull");
        _photos.Verify(p => p.UploadPhotoAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<BlobUploadContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_ShouldDropTheBlob_WhenTheStoredPictureWouldPassTheCap()
    {
        var project = Given();
        _projects.Setup(r => r.GetStoredBytesAsync(_memberKey, ProbeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProbeProjectRules.MaxStoredBytes - 10);
        _photos.Setup(p => p.UploadPhotoAsync(It.IsAny<byte[]>(), "a.png", BlobUploadContext.ProbeProjectImage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoUploadResult("probe-project-images/big.jpg", "ignored", 100));

        var result = await UploadHandler().Handle(
            new UploadProbeProjectImageCommand(_memberKey, ProbeId, project.ProbeProjectKey, new byte[100], "a.png"), CancellationToken.None);

        result.ErrorCode.Should().Be("StorageFull");
        _photos.Verify(p => p.DeletePhotoAsync("probe-project-images/big.jpg", It.IsAny<CancellationToken>()), Times.Once);
        _projects.Verify(r => r.AddImage(It.IsAny<ProbeProjectImage>()), Times.Never);
    }

    [Fact]
    public async Task Upload_ShouldCountWhatIsStored_NotWhatWasSent()
    {
        var project = Given();
        _projects.Setup(r => r.GetStoredBytesAsync(_memberKey, ProbeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProbeProjectRules.MaxStoredBytes - 50);
        _photos.Setup(p => p.UploadPhotoAsync(It.IsAny<byte[]>(), "a.png", BlobUploadContext.ProbeProjectImage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoUploadResult("probe-project-images/small.jpg", "ignored", 40));

        var result = await UploadHandler().Handle(
            new UploadProbeProjectImageCommand(_memberKey, ProbeId, project.ProbeProjectKey, new byte[1000], "a.png"), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _projects.Verify(r => r.AddImage(It.Is<ProbeProjectImage>(i => i.Bytes == 40)), Times.Once);
    }

    [Fact]
    public async Task LimitedWrites_ShouldLockThePersonsProbe_BeforeCounting()
    {
        var order = new List<string>();
        _projects.Setup(r => r.LockAsync(_memberKey, ProbeId, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("lock")).Returns(Task.CompletedTask);
        _projects.Setup(r => r.GetForMemberProbeAsync(_memberKey, ProbeId, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("count")).ReturnsAsync(() => _stored.ToList());

        await SaveHandler().Handle(new SaveProbeProjectCommand(_memberKey, ProbeId, null, Input()), CancellationToken.None);

        order.Should().StartWith(["lock", "count"]);
        new object[]
        {
            new SaveProbeProjectCommand(_memberKey, ProbeId, null, Input()),
            new UploadProbeProjectImageCommand(_memberKey, ProbeId, Guid.NewGuid(), [], "a.png"),
            new UploadCampCertificateCommand(_memberKey, ProbeId, [], "a.pdf", true)
        }.Should().AllBeAssignableTo<ProjectK.BusinessLogic.Behaviors.ITransactionalRequest>();
    }

    [Fact]
    public async Task Certificate_ShouldCountItsBytes_AndNotLetThemPassTheCap()
    {
        _projects.Setup(r => r.GetStoredBytesAsync(_memberKey, ProbeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProbeProjectRules.MaxStoredBytes);

        var result = await CertificateHandler().Handle(
            new UploadCampCertificateCommand(_memberKey, ProbeId, [1, 2, 3], "scan.jpg", IsPdf: false), CancellationToken.None);

        result.ErrorCode.Should().Be("StorageFull");
    }

    [Fact]
    public async Task Stages_ShouldBeSetByWhoeverMaySign_AndJournaled()
    {
        var handler = new SetThirdProbeStagesCommandHandler(_unitOfWork.Object, _access.Object, _journal.Actors, _journal.Audit, _probes.Object, _reader.Object);
        var stages = new ThirdProbeStagesInput(new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 20), new DateOnly(2025, 10, 1), new DateOnly(2025, 11, 1), null, null, null);

        var result = await handler.Handle(new SetThirdProbeStagesCommand(_memberKey, ProbeId, stages), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _file!.ApplicationSubmittedOn.Should().Be(new DateOnly(2025, 10, 1));
        _file.OpenedOn.Should().Be(new DateOnly(2025, 11, 1));
        _journal.Entries.Should().ContainSingle(e => e.Action == AuditActions.Probe.StagesChanged);
    }

    [Theory]
    [InlineData("2026-01-01", "2025-12-01", null)]
    [InlineData("2026-01-01", null, "2026-02-01")]
    [InlineData("2026-01-01", "2026-03-01", "2026-02-01")]
    public async Task Stages_ShouldRefuseAClosingBeforeTheOpeningOrANamingBeforeTheClosing(string opened, string? completed, string? named)
    {
        var handler = new SetThirdProbeStagesCommandHandler(_unitOfWork.Object, _access.Object, _journal.Actors, _journal.Audit, _probes.Object, _reader.Object);
        var stages = new ThirdProbeStagesInput(null, null, null, DateOnly.Parse(opened),
            completed is null ? null : DateOnly.Parse(completed), named is null ? null : DateOnly.Parse(named), null);

        var result = await handler.Handle(new SetThirdProbeStagesCommand(_memberKey, ProbeId, stages), CancellationToken.None);

        result.ErrorCode.Should().Be("StagesOutOfOrder");
    }

    [Fact]
    public async Task Stages_ShouldBeForbidden_ToTheYouthThemself()
    {
        _access.Setup(a => a.CanSignAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var handler = new SetThirdProbeStagesCommandHandler(_unitOfWork.Object, _access.Object, _journal.Actors, _journal.Audit, _probes.Object, _reader.Object);

        var result = await handler.Handle(
            new SetThirdProbeStagesCommand(_memberKey, ProbeId, new ThirdProbeStagesInput(null, null, null, null, null, null, null)), CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
    }

    private static ThirdProbeFileInput FileInput(DateOnly? reflectionOn = null) =>
        new(null, null, null, reflectionOn, null, null, null, null, null, null, "");

    [Fact]
    public async Task Certificate_ShouldRefuseAFileThatIsNotAPictureOrAPdf()
    {
        _photos.Setup(p => p.UploadPhotoAsync(It.IsAny<byte[]>(), It.IsAny<string>(), BlobUploadContext.ThirdProbeCertificatePdf, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("not a PDF"));

        var result = await CertificateHandler().Handle(
            new UploadCampCertificateCommand(_memberKey, ProbeId, [1, 2, 3], "posvidka.pdf", IsPdf: true), CancellationToken.None);

        result.ErrorCode.Should().Be("InvalidCertificate");
        _file.Should().BeNull();
    }

    [Fact]
    public async Task Certificate_ShouldReplaceThePreviousOne_AndDeleteItsFile()
    {
        _file = new ThirdProbeFile { MemberKey = _memberKey, ProbeId = ProbeId, CampCertificateBlobName = "third-probe-certificates/old.jpg" };
        _photos.Setup(p => p.UploadPhotoAsync(It.IsAny<byte[]>(), "posvidka.pdf", BlobUploadContext.ThirdProbeCertificatePdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoUploadResult("third-probe-certificates/new.pdf", "ignored"));

        var result = await CertificateHandler().Handle(
            new UploadCampCertificateCommand(_memberKey, ProbeId, [37, 80, 68, 70, 45, 49], "posvidka.pdf", IsPdf: true), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _file.CampCertificateBlobName.Should().Be("third-probe-certificates/new.pdf");
        _file.CampCertificateContentType.Should().Be("application/pdf");
        _photos.Verify(p => p.DeletePhotoAsync("third-probe-certificates/old.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Certificate_ShouldRefuseAFileOverTwoMegabytes()
    {
        var result = await CertificateHandler().Handle(
            new UploadCampCertificateCommand(_memberKey, ProbeId, new byte[ProbeProjectRules.MaxCertificateBytes + 1], "scan.jpg", IsPdf: false),
            CancellationToken.None);

        result.ErrorCode.Should().Be("CertificateTooLarge");
    }

    [Fact]
    public void Markdown_ShouldReadThePictureKeys_AcrossTexts()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        ProbeProjectMarkdown.ImageKeys($"a ![x](project-image:{first})", $"[link](https://plast.org.ua) ![](project-image:{second})")
            .Should().Equal(first, second);
    }
}
