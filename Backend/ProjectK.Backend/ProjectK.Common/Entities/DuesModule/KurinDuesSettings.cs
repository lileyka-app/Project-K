using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.DuesModule;

/// <summary>
/// How a kurin's вкладка works, where that is one choice rather than a rate. A kurin with no row pays
/// <see cref="DuesChargeTiming.InAdvance"/>, the way Лілейка charged before the choice existed.
/// </summary>
public class KurinDuesSettings : Entity
{
    public Guid KurinDuesSettingsKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    public DuesChargeTiming ChargeTiming { get; set; } = DuesChargeTiming.InAdvance;

    public Guid? SetByUserKey { get; set; }
}
