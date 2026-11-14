using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.DuesModule;

/// <summary>
/// One kurin in a row. <see cref="Number"/> is its own in its улад, so <see cref="Branch"/> and
/// <see cref="Branches"/> tell 5-й курінь УПЮ from 5-й курінь УСП.
/// </summary>
public sealed record KraiConcessionReportKurinDto
{
    public required Guid KurinKey { get; init; }
    public required int? Number { get; init; }
    public required KurinBranch Branch { get; init; }
    public required IReadOnlyList<KurinNumbering> Branches { get; init; }
    public required string? NamedAfter { get; init; }
    public required int People { get; init; }
    public required decimal Reduction { get; init; }
}
