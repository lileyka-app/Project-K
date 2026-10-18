using Moq;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.KraiProfiles;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>A край for the dues rules: Czech money and clock, and the kurins hand up the way <c>upstream</c> says.</summary>
public static class KraiProfiles
{
    public static IKraiProfileProvider Paying(Guid kurinKey, string upstream)
    {
        var profiles = new Mock<IKraiProfileProvider>();
        profiles.Setup(p => p.GetForKurinAsync(kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(new KraiProfile(
            "cz", "Чехія", 1, new KraiCatalogSettings(["cz"], []), new KraiLocaleSettings("CZK", "Europe/Prague", "cs-CZ"),
            KraiCardFieldsSettings.Default, new KraiDuesSettings(upstream)));
        return profiles.Object;
    }
}
