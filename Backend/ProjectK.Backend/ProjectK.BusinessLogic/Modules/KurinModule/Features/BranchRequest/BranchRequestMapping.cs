using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using KurinEntity = ProjectK.Common.Entities.KurinModule.Kurin;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;

/// <summary>A kurin's request to change its гілки, as the kurin and the administrator read it.</summary>
public static class BranchRequestMapping
{
    public static KurinBranchRequestDto ToDto(this KurinBranchRequest request, KurinEntity kurin, string? requestedByName) => new(
        KurinBranchRequestKey: request.KurinBranchRequestKey,
        KurinKey: kurin.KurinKey,
        KraiCode: kurin.KraiCode,
        KurinNamedAfter: kurin.NamedAfter,
        CurrentBranches: kurin.Numbering,
        WantedBranches: request.WantedBranches,
        Note: request.Note,
        Status: request.Status,
        RequestedByName: requestedByName,
        RequestedAtUtc: request.RequestedAtUtc);
}
