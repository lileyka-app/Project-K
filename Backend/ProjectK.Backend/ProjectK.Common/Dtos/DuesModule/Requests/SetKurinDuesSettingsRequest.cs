using ProjectK.Common.Enums.DuesModule;

namespace ProjectK.Common.Dtos.DuesModule.Requests;

/// <summary>Body of <c>PUT api/kurin/{kurinKey}/dues/settings</c>.</summary>
public sealed class SetKurinDuesSettingsRequest
{
    public DuesChargeTiming ChargeTiming { get; set; }
}
