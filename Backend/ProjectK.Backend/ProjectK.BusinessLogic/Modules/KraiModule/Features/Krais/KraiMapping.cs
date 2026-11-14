using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Models.Dtos.KraiModule;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Krais;

internal static class KraiMapping
{
    public static KraiDto ToDto(this Krai krai, int kurinCount) => new(
        Code: krai.Code,
        Name: krai.Name,
        KurinCount: kurinCount);

    /// <summary>The край for a picker, marked when it is <paramref name="defaultCode"/>, the one a kurin opens in when nobody picks one.</summary>
    public static KraiNameDto ToNameDto(this Krai krai, string? defaultCode) => new(
        Code: krai.Code,
        Name: krai.Name,
        IsDefault: krai.Code == defaultCode);
}
