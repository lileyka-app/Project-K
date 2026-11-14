using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Models.Dtos.KraiModule;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Krais;

internal static class KraiMapping
{
    public static KraiDto ToDto(this Krai krai, int kurinCount) => new(
        Code: krai.Code,
        Name: krai.Name,
        KurinCount: kurinCount);

    /// <param name="defaultCode">The край a kurin opens in when nobody picks one.</param>
    public static KraiNameDto ToNameDto(this Krai krai, string? defaultCode) => new(
        Code: krai.Code,
        Name: krai.Name,
        IsDefault: krai.Code == defaultCode);
}
