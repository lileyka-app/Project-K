using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.SetSettings;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

/// <summary>A kurin chooses once when its вкладка falls due; the choice is one row, kept and rewritten.</summary>
public class SetKurinDuesSettingsTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private readonly Mock<IKurinDuesSettingsRepository> _settings = new();
    private readonly Mock<IDuesUnitOfWork> _dues = new();
    private readonly Mock<ICurrentUserContext> _user = new();

    public SetKurinDuesSettingsTests()
    {
        _dues.SetupGet(d => d.KurinDuesSettings).Returns(_settings.Object);
        _user.SetupGet(u => u.KurinKey).Returns(Kurin);
    }

    private SetKurinDuesSettingsCommandHandler Handler() => new(_dues.Object, _user.Object);

    [Fact]
    public async Task FirstChoice_IsStoredAsTheKurinsRow()
    {
        KurinDuesSettings? created = null;
        _settings.Setup(s => s.Create(It.IsAny<KurinDuesSettings>(), It.IsAny<CancellationToken>()))
            .Callback<KurinDuesSettings, CancellationToken>((row, _) => created = row);

        var result = await Handler().Handle(new SetKurinDuesSettingsCommand(Kurin, DuesChargeTiming.InArrears), default);

        result.Type.Should().Be(ResultType.Success);
        created!.KurinKey.Should().Be(Kurin);
        created.ChargeTiming.Should().Be(DuesChargeTiming.InArrears);
    }

    [Fact]
    public async Task ALaterChoice_RewritesTheSameRow()
    {
        var row = new KurinDuesSettings { KurinKey = Kurin, ChargeTiming = DuesChargeTiming.InArrears };
        _settings.Setup(s => s.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(row);

        await Handler().Handle(new SetKurinDuesSettingsCommand(Kurin, DuesChargeTiming.InAdvance), default);

        row.ChargeTiming.Should().Be(DuesChargeTiming.InAdvance);
        _settings.Verify(s => s.Create(It.IsAny<KurinDuesSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnotherKurin_IsRefused()
    {
        var result = await Handler().Handle(new SetKurinDuesSettingsCommand(Guid.NewGuid(), DuesChargeTiming.InArrears), default);

        result.Type.Should().Be(ResultType.Forbidden);
        _dues.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
