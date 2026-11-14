using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>
/// A kurin's request to change its гілки, as the kurin and the administrator see it.
/// <c>CurrentBranches</c> is what the kurin has now, main гілка first; <c>WantedBranches</c> is what it
/// asks to have instead.
/// </summary>
public sealed record KurinBranchRequestDto(
    Guid KurinBranchRequestKey,
    Guid KurinKey,
    string KraiCode,
    string? KurinNamedAfter,
    IReadOnlyList<KurinNumbering> CurrentBranches,
    IReadOnlyList<KurinNumbering> WantedBranches,
    string? Note,
    KurinBranchRequestStatus Status,
    string? RequestedByName,
    DateTime RequestedAtUtc);
