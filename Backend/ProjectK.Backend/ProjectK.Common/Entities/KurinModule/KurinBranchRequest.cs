using System;
using System.Collections.Generic;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Entities.KurinModule;

/// <summary>
/// A senior kurin asking an administrator to change its гілки: add УПС beside УСП, drop one, or give
/// one a different number. The kurin cannot do it itself — a number in a гілка of the край is
/// something the admin hands out, as when the kurin first opened. Carries the whole wanted set, not a
/// difference, so approving it is one write whatever happened in between.
/// </summary>
public class KurinBranchRequest : Entity
{
    public Guid KurinBranchRequestKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    /// <summary>The account that sent it; told of the decision.</summary>
    public Guid RequestedByUserKey { get; set; }

    /// <summary>The wanted number in УСП; null leaves the kurin out of УСП.</summary>
    public int? UspNumber { get; set; }

    /// <summary>The wanted number in УПС; null leaves the kurin out of УПС.</summary>
    public int? UpsNumber { get; set; }

    public const int NoteMaxLength = 1000;

    /// <summary>What the kurin wrote to the admin, if anything; up to <see cref="NoteMaxLength"/> characters.</summary>
    public string? Note { get; set; }

    public KurinBranchRequestStatus Status { get; set; } = KurinBranchRequestStatus.Pending;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserKey { get; set; }

    /// <summary>The гілки the kurin asks to have, УСП first.</summary>
    public IReadOnlyList<KurinNumbering> WantedBranches =>
    [
        .. UspNumber is int usp ? [new KurinNumbering(KurinBranch.USP, usp)] : Array.Empty<KurinNumbering>(),
        .. UpsNumber is int ups ? [new KurinNumbering(KurinBranch.UPS, ups)] : Array.Empty<KurinNumbering>()
    ];
}

/// <summary>Stored as a number: append new members at the end.</summary>
public enum KurinBranchRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
