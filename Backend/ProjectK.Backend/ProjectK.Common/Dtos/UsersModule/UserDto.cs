using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Dtos.UsersModule;

/// <summary>An account as the admin list shows it: the system role, the kurin it stands in, and its state.</summary>
public sealed record UserDto
{
    public required Guid UserId { get; init; }
    public required Guid? KurinKey { get; init; }
    public required int? KurinNumber { get; init; }

    /// <summary>
    /// The kurin's main улад. Each улад counts its own numbers, so the number alone would put
    /// 5-й курінь УПЮ and 5-й курінь УСП under one heading.
    /// </summary>
    public required KurinBranch? KurinBranch { get; init; }
    public required IReadOnlyList<KurinNumbering>? KurinBranches { get; init; }
    public required string? KurinNamedAfter { get; init; }

    /// <summary>The kurin's край: numbers are the kurin's own per край too, so two УПЮ kurins may share one.</summary>
    public required string? KurinKraiCode { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required bool TwoFactorEnabled { get; init; }

    /// <summary>Refused at sign-in by an administrator; every session was ended when it was applied.</summary>
    public required bool IsSuspended { get; init; }
}
