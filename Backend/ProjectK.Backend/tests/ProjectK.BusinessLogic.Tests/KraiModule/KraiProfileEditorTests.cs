using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KraiModule;

public class KraiProfileEditorTests
{
    private readonly Mock<IKraiRepository> _krais = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IBackendCache> _cache = new();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly List<KraiProfileRevision> _added = [];
    private readonly KraiProfileEditor _editor;

    public KraiProfileEditorTests()
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(_admin);
        _krais.Setup(r => r.GetAsync("cz", It.IsAny<CancellationToken>())).ReturnsAsync(new Krai { Code = "cz", Name = "Чехія" });
        _krais.Setup(r => r.AddProfileRevision(It.IsAny<KraiProfileRevision>())).Callback<KraiProfileRevision>(_added.Add);
        _unitOfWork.Setup(u => u.Krais).Returns(_krais.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _editor = new KraiProfileEditor(_unitOfWork.Object, user.Object, _cache.Object);
    }

    private void Latest(int revision, string json = "{}")
    {
        var latest = new KraiProfileRevision { KraiCode = "cz", Revision = revision, Json = json };
        _krais.Setup(r => r.GetLatestProfileRevisionAsync("cz", It.IsAny<CancellationToken>())).ReturnsAsync(latest);
        _krais.Setup(r => r.GetProfileRevisionAsync("cz", revision, It.IsAny<CancellationToken>())).ReturnsAsync(latest);
    }

    private static KraiProfileDocument Sources(params string[] codes) =>
        new() { Catalog = new KraiCatalogDocument { BadgeSources = codes } };

    [Fact]
    public async Task Saving_AddsTheNextRevision_Normalized()
    {
        Latest(1);

        var result = await _editor.SaveAsync(" CZ ", 1, new KraiProfileDocument
        {
            Catalog = new KraiCatalogDocument { BadgeSources = [" CZ ", "ua", "cz", ""], HiddenBadges = [] },
            Locale = new KraiLocaleDocument { Currency = "czk" },
            Dues = new KraiDuesDocument { Upstream = " Krai " }
        }, " перше налаштування ", default);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.Revision.Should().Be(2);
        var saved = _added.Single();
        saved.Revision.Should().Be(2);
        saved.CreatedByUserKey.Should().Be(_admin);
        saved.Note.Should().Be("перше налаштування");
        saved.Json.Should().Be("""{"catalog":{"badgeSources":["cz","ua"]},"locale":{"currency":"CZK"},"dues":{"upstream":"krai"}}""");
        _cache.Verify(c => c.Invalidate(BackendCachePolicies.KraiProfileReads), Times.Once);
    }

    [Fact]
    public async Task Saving_OverSomeoneElsesSave_IsRefused()
    {
        Latest(3);

        var result = await _editor.SaveAsync("cz", 2, Sources("cz"), null, default);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("ProfileChanged");
        _added.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{"catalog":{"badgeSources":["c z"]}}""")]
    [InlineData("""{"locale":{"currency":"korun"}}""")]
    [InlineData("""{"locale":{"timeZone":"Mars/Olympus"}}""")]
    [InlineData("""{"locale":{"numberLocale":"xx-NOPE"}}""")]
    [InlineData("""{"dues":{"upstream":"oblast"}}""")]
    [InlineData("""{"cardFields":{"fields":{"email":"hidden"}}}""")]
    [InlineData("""{"cardFields":{"fields":{"middleName":"sometimes"}}}""")]
    [InlineData("""{"cardFields":{"extra":[{"key":"тип","label":"Тип візи"}]}}""")]
    [InlineData("""{"cardFields":{"extra":[{"key":"visa","label":""}]}}""")]
    [InlineData("""{"cardFields":{"extra":[{"key":"visa","label":"Віза","type":"number"}]}}""")]
    [InlineData("""{"cardFields":{"extra":[{"key":"visa","label":"Віза"},{"key":"visa","label":"Ще"}]}}""")]
    public async Task AnInvalidDocument_IsRefused(string json)
    {
        Latest(1);

        var result = await _editor.SaveAsync("cz", 1, KraiProfileJson.Parse(json), null, default);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("InvalidProfile");
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task AnUnknownKrai_IsNotFound()
    {
        var result = await _editor.SaveAsync("xx", 0, Sources("xx"), null, default);

        result.Type.Should().Be(ResultType.NotFound);
    }

    [Fact]
    public async Task Restoring_SavesTheOldDocumentAsTheNewest()
    {
        var old = new KraiProfileRevision { KraiCode = "cz", Revision = 1, Json = """{"catalog":{"badgeSources":["ua"]}}""" };
        _krais.Setup(r => r.GetProfileRevisionAsync("cz", 1, It.IsAny<CancellationToken>())).ReturnsAsync(old);
        Latest(4);

        var result = await new RestoreKraiProfileRevisionCommandHandler(_unitOfWork.Object, _editor)
            .Handle(new RestoreKraiProfileRevisionCommand("cz", 1), default);

        result.Type.Should().Be(ResultType.Success);
        var saved = _added.Single();
        saved.Revision.Should().Be(5);
        saved.Json.Should().Be(old.Json);
        saved.Note.Should().Be("Відновлено ревізію 1");
    }

    [Fact]
    public async Task RestoringAMissingRevision_IsNotFound()
    {
        var result = await new RestoreKraiProfileRevisionCommandHandler(_unitOfWork.Object, _editor)
            .Handle(new RestoreKraiProfileRevisionCommand("cz", 9), default);

        result.Type.Should().Be(ResultType.NotFound);
    }
}
