using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.AccessPolicy;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.KurinHandlers;

public class KurinAccessPolicyHandlerTests
{
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IBackendCache> _cache = new();
    private readonly Mock<IAccessChangeSignal> _accessChanges = new();
    private readonly KurinAccessPolicyProvider _provider;
    private readonly Kurin _kurin = new(5);

    public KurinAccessPolicyHandlerTests()
    {
        _cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<CachePolicy>(),
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<KurinAccessPolicy>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<CacheScopeContext?>()))
            .Returns((CachePolicy _, string _, Func<CancellationToken, Task<KurinAccessPolicy>> factory, CancellationToken token, CacheScopeContext? _) => factory(token));

        _unitOfWork.Setup(u => u.Kurins).Returns(_kurins.Object);
        _kurins.Setup(r => r.GetByKeyAsync(_kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(_kurin);
        _provider = new KurinAccessPolicyProvider(_unitOfWork.Object, _cache.Object, NullLogger<KurinAccessPolicyProvider>.Instance);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string?>> Matrix(
        params (string Capability, string Audience, string? Reach)[] cells) =>
        cells
            .GroupBy(cell => cell.Capability)
            .ToDictionary(
                row => row.Key,
                row => (IReadOnlyDictionary<string, string?>)row.ToDictionary(cell => cell.Audience, cell => cell.Reach));

    // What the settings page sends back unchanged: every cell at its effective value.
    private static Dictionary<string, IReadOnlyDictionary<string, string?>> Echo(KurinAccessPolicyDto dto) =>
        dto.Capabilities.ToDictionary(
            row => row.Key.ToString(),
            row => (IReadOnlyDictionary<string, string?>)row.Cells.ToDictionary(
                cell => cell.Audience.ToString(),
                cell => (string?)cell.Effective.ToString()));

    private static AccessPolicyCellDto Cell(KurinAccessPolicyDto dto, KurinCapability capability, PolicyAudience audience) =>
        dto.Capabilities.Single(row => row.Key == capability).Cells.Single(cell => cell.Audience == audience);

    private SetKurinAccessPolicyCommandHandler SetHandler() => new(_unitOfWork.Object, _provider, _accessChanges.Object);

    [Fact]
    public async Task Get_GivesEveryCell_WithItsDefaultAndLock()
    {
        _kurin.AccessPolicy = """{"v":1,"grants":{"SeeContacts":{"Youth":"OwnGroup"}}}""";

        var result = await new GetKurinAccessPolicyQueryHandler(_unitOfWork.Object, _provider)
            .Handle(new GetKurinAccessPolicyQuery(_kurin.KurinKey), default);

        result.Type.Should().Be(ResultType.Success);
        var dto = result.Data!;
        dto.Audiences.Should().Equal(Enum.GetValues<PolicyAudience>());
        dto.Capabilities.Select(row => row.Key).Should().Equal(Enum.GetValues<KurinCapability>());
        dto.Capabilities.Should().OnlyContain(row => row.Cells.Count == dto.Audiences.Count);

        Cell(dto, KurinCapability.SeeContacts, PolicyAudience.Youth)
            .Should().Be(new AccessPolicyCellDto(PolicyAudience.Youth, CapabilityReach.OwnGroup, CapabilityReach.Kurin, false));
        Cell(dto, KurinCapability.SeeContacts, PolicyAudience.Zvyazkovyi).Locked.Should().BeTrue();
        Cell(dto, KurinCapability.GiveWarning1, PolicyAudience.Youth).Locked.Should().BeTrue();
        Cell(dto, KurinCapability.GiveWarning1, PolicyAudience.Vykhovnyk)
            .Should().Be(new AccessPolicyCellDto(PolicyAudience.Vykhovnyk, CapabilityReach.OwnGroup, CapabilityReach.OwnGroup, false));
        dto.Capabilities.Single(row => row.Key == KurinCapability.SeeAddress).YouthConfigurable.Should().BeTrue();
        dto.Capabilities.Single(row => row.Key == KurinCapability.SignProbe).YouthConfigurable.Should().BeFalse();
    }

    [Fact]
    public async Task Get_AMissingKurin_IsNotFound()
    {
        var result = await new GetKurinAccessPolicyQueryHandler(_unitOfWork.Object, _provider)
            .Handle(new GetKurinAccessPolicyQuery(Guid.NewGuid()), default);

        result.Type.Should().Be(ResultType.NotFound);
        result.ErrorCode.Should().Be("KurinNotFound");
    }

    [Fact]
    public async Task Set_StoresOnlyWhatDiffersFromTheDefaults_DropsTheCache_AndMarksTheKurin()
    {
        var current = await new GetKurinAccessPolicyQueryHandler(_unitOfWork.Object, _provider)
            .Handle(new GetKurinAccessPolicyQuery(_kurin.KurinKey), default);
        var matrix = Echo(current.Data!);
        matrix["SeeBirthDate"] = new Dictionary<string, string?>(matrix["SeeBirthDate"]) { ["Youth"] = "None", ["Hurtkovyy"] = "OwnGroup" };

        var result = await SetHandler().Handle(new SetKurinAccessPolicyCommand(_kurin.KurinKey, matrix), default);

        result.Type.Should().Be(ResultType.Success);
        _kurin.AccessPolicy.Should().Be("""{"v":1,"grants":{"SeeBirthDate":{"Hurtkovyy":"OwnGroup","Youth":"None"}}}""");
        Cell(result.Data!, KurinCapability.SeeBirthDate, PolicyAudience.Youth).Effective.Should().Be(CapabilityReach.None);
        _kurins.Verify(r => r.Update(_kurin, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.Invalidate(BackendCachePolicies.KurinAccessPolicyReads), Times.Once);
        _accessChanges.Verify(s => s.MarkKurin(_kurin.KurinKey), Times.Once);
    }

    [Fact]
    public async Task Set_TheDefaults_ClearsTheColumn()
    {
        _kurin.AccessPolicy = """{"v":1,"grants":{"SeeContacts":{"Youth":"OwnGroup"}}}""";

        var result = await SetHandler().Handle(
            new SetKurinAccessPolicyCommand(_kurin.KurinKey, Matrix(("SeeContacts", "Youth", "Kurin"))), default);

        result.Type.Should().Be(ResultType.Success);
        _kurin.AccessPolicy.Should().BeNull();
    }

    [Theory]
    [InlineData("SeeContacts", "Zvyazkovyi", "OwnGroup")]
    [InlineData("ConfirmAward2", "Youth", "Kurin")]
    [InlineData("SeeEverything", "Youth", "Kurin")]
    [InlineData("SeeContacts", "Youth", "Everyone")]
    public async Task Set_AnInvalidMatrix_IsRefused_AndNothingIsSaved(string capability, string audience, string reach)
    {
        var result = await SetHandler().Handle(
            new SetKurinAccessPolicyCommand(_kurin.KurinKey, Matrix((capability, audience, reach))), default);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be(KurinAccessPolicy.InvalidErrorCode);
        _kurin.AccessPolicy.Should().BeNull();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.Invalidate(It.IsAny<CachePolicy>()), Times.Never);
        _accessChanges.Verify(s => s.MarkKurin(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Set_AMissingKurin_IsNotFound()
    {
        var result = await SetHandler().Handle(
            new SetKurinAccessPolicyCommand(Guid.NewGuid(), Matrix(("SeeContacts", "Youth", "None"))), default);

        result.Type.Should().Be(ResultType.NotFound);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Provider_ReadsTheKurinsColumn_ThroughItsCache()
    {
        _kurin.AccessPolicy = """{"v":1,"grants":{"ReadJournal":{"Kurinnyy":"Kurin"}}}""";

        var policy = await _provider.GetAsync(_kurin.KurinKey);

        policy.Reach(KurinCapability.ReadJournal, PolicyAudience.Kurinnyy).Should().Be(CapabilityReach.Kurin);
        _cache.Verify(c => c.GetOrCreateAsync(
            BackendCachePolicies.KurinAccessPolicyReads,
            _kurin.KurinKey.ToString(),
            It.IsAny<Func<CancellationToken, Task<KurinAccessPolicy>>>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<CacheScopeContext?>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ broken")]
    [InlineData("""{"v":9,"grants":{}}""")]
    public async Task Provider_GivesTheDefaults_ForADocumentItCannotUse(string? json)
    {
        _kurin.AccessPolicy = json;

        (await _provider.GetAsync(_kurin.KurinKey)).IsDefault.Should().BeTrue();
        (await _provider.GetAsync(Guid.NewGuid())).IsDefault.Should().BeTrue();
    }
}
