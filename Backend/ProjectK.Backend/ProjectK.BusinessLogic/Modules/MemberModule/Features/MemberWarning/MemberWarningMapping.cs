using ProjectK.Common.Dtos.MemberModule;
using MemberWarningEntity = ProjectK.Common.Entities.MemberModule.MemberWarning;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.MemberWarning;

/// <summary>A пересторога as the card reads it.</summary>
public static class MemberWarningMapping
{
    public static MemberWarningDto ToDto(this MemberWarningEntity warning) => new()
    {
        MemberWarningKey = warning.MemberWarningKey,
        MemberKey = warning.MemberKey,
        Level = warning.Level,
        IssuedAtUtc = warning.IssuedAtUtc,
        ExpiresAtUtc = warning.ExpiresAtUtc,
        IssuedByUserKey = warning.IssuedByUserKey,
        RevokedByUserKey = warning.RevokedByUserKey,
        RevokedAtUtc = warning.RevokedAtUtc
    };
}
