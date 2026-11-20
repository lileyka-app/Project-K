using Moq;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Dtos.KraiModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>A край for the dues rules: Czech money and clock, and the kurins hand up the way <c>upstream</c> says.</summary>
public static class KraiProfiles
{
    public static IKraiProfileDirectory Paying(Guid kurinKey, string upstream)
    {
        var profiles = new Mock<IKraiProfileDirectory>();
        profiles.Setup(p => p.GetForKurinAsync(kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(new KraiProfile(
            "cz", "Чехія", 1, new KraiCatalogSettings(["cz"], []), new KraiLocaleSettings("CZK", "Europe/Prague", "cs-CZ"),
            KraiCardFieldsSettings.Default, new KraiDuesSettings(upstream)));
        return profiles.Object;
    }
}
