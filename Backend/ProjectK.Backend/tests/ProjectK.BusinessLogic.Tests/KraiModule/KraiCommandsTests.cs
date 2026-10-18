using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KraiModule.Features.Manage;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KraiModule;

public class KraiCommandsTests
{
    private readonly Mock<IKraiRepository> _krais = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IBackendCache> _cache = new();
    private readonly Mock<ICurrentUserContext> _user = new();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly List<Krai> _addedKrais = [];
    private readonly List<KraiProfileRevision> _addedRevisions = [];

    public KraiCommandsTests()
    {
        _user.SetupGet(u => u.UserId).Returns(_admin);
        _krais.Setup(r => r.Add(It.IsAny<Krai>())).Callback<Krai>(_addedKrais.Add);
        _krais.Setup(r => r.AddProfileRevision(It.IsAny<KraiProfileRevision>())).Callback<KraiProfileRevision>(_addedRevisions.Add);
        _kurins.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _unitOfWork.Setup(u => u.Krais).Returns(_krais.Object);
        _unitOfWork.Setup(u => u.Kurins).Returns(_kurins.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private CreateKraiCommandHandler Create() => new(_unitOfWork.Object, _user.Object, _cache.Object);

    [Fact]
    public async Task Creating_AddsTheKraiWithAFirstRevision_ReadingUkrainesBadges()
    {
        var result = await Create().Handle(new CreateKraiCommand(" SK ", " Словаччина ", null), default);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.Code.Should().Be("sk");
        _addedKrais.Single().Name.Should().Be("Словаччина");
        var revision = _addedRevisions.Single();
        revision.KraiCode.Should().Be("sk");
        revision.Revision.Should().Be(1);
        revision.CreatedByUserKey.Should().Be(_admin);
        revision.Json.Should().Be("""{"catalog":{"badgeSources":["ua"]}}""");
        _cache.Verify(c => c.Invalidate(BackendCachePolicies.KraiProfileReads), Times.Once);
    }

    [Fact]
    public async Task Creating_KeepsTheProfileItIsGiven()
    {
        var document = new KraiProfileDocument { Locale = new KraiLocaleDocument { Currency = "eur", TimeZone = "Europe/Bratislava" } };

        await Create().Handle(new CreateKraiCommand("sk", "Словаччина", document), default);

        _addedRevisions.Single().Json.Should().Be("""{"locale":{"currency":"EUR","timeZone":"Europe/Bratislava"}}""");
    }

    [Theory]
    [InlineData("s")]
    [InlineData("sk-1")]
    [InlineData("словаччина")]
    public async Task Creating_WithABadCode_IsRefused(string code)
    {
        var result = await Create().Handle(new CreateKraiCommand(code, "Словаччина", null), default);

        result.Type.Should().Be(ResultType.BadRequest);
        _addedKrais.Should().BeEmpty();
    }

    [Fact]
    public async Task Creating_ATakenCode_IsAConflict()
    {
        _krais.Setup(r => r.ExistsAsync("pl", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Create().Handle(new CreateKraiCommand("pl", "Польща", null), default);

        result.Type.Should().Be(ResultType.Conflict);
        _addedKrais.Should().BeEmpty();
    }

    [Fact]
    public async Task Creating_WithAnInvalidProfile_SavesNothing()
    {
        var document = new KraiProfileDocument { Locale = new KraiLocaleDocument { TimeZone = "Mars/Olympus" } };

        var result = await Create().Handle(new CreateKraiCommand("sk", "Словаччина", document), default);

        result.Type.Should().Be(ResultType.BadRequest);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Renaming_ChangesTheNameOnly()
    {
        var krai = new Krai { Code = "cz", Name = "Чехія" };
        _krais.Setup(r => r.GetForUpdateAsync("cz", It.IsAny<CancellationToken>())).ReturnsAsync(krai);
        _kurins.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new Kurin(5) { KraiCode = "cz" }]);

        var result = await new RenameKraiCommandHandler(_unitOfWork.Object, _cache.Object)
            .Handle(new RenameKraiCommand(" CZ ", " Пласт Чехія "), default);

        result.Type.Should().Be(ResultType.Success);
        krai.Name.Should().Be("Пласт Чехія");
        result.Data!.KurinCount.Should().Be(1);
        _cache.Verify(c => c.Invalidate(BackendCachePolicies.KraiProfileReads), Times.Once);
    }

    [Fact]
    public async Task Renaming_AnUnknownKrai_IsNotFound()
    {
        var result = await new RenameKraiCommandHandler(_unitOfWork.Object, _cache.Object)
            .Handle(new RenameKraiCommand("xx", "Ніде"), default);

        result.Type.Should().Be(ResultType.NotFound);
    }
}
