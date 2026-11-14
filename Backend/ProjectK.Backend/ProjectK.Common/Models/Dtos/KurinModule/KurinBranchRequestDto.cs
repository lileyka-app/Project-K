using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>A kurin's request to change its гілки, as the kurin and the administrator see it.</summary>
/// <param name="CurrentBranches">What the kurin has now, main гілка first.</param>
/// <param name="WantedBranches">What it asks to have instead.</param>
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
