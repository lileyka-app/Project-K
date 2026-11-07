using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

/// <summary>A kurin's request to change its гілки, as the kurin and the administrator see it.</summary>
public sealed class KurinBranchRequestResponse
{
    public Guid KurinBranchRequestKey { get; set; }
    public Guid KurinKey { get; set; }
    public string KraiCode { get; set; } = string.Empty;
    public string? KurinNamedAfter { get; set; }

    /// <summary>What the kurin has now, main гілка first.</summary>
    public IReadOnlyList<KurinNumbering> CurrentBranches { get; set; } = [];

    /// <summary>What it asks to have instead.</summary>
    public IReadOnlyList<KurinNumbering> WantedBranches { get; set; } = [];
    public string? Note { get; set; }
    public KurinBranchRequestStatus Status { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime RequestedAtUtc { get; set; }

    public static KurinBranchRequestResponse From(KurinBranchRequest request, Kurin kurin, string? requestedByName) => new()
    {
        KurinBranchRequestKey = request.KurinBranchRequestKey,
        KurinKey = kurin.KurinKey,
        KraiCode = kurin.KraiCode,
        KurinNamedAfter = kurin.NamedAfter,
        CurrentBranches = kurin.Numbering,
        WantedBranches = request.WantedBranches,
        Note = request.Note,
        Status = request.Status,
        RequestedByName = requestedByName,
        RequestedAtUtc = request.RequestedAtUtc
    };
}
