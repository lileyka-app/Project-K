using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Dtos.DuesModule;

/// <summary>What the person owes or has over in one of their other kurins, named by their own line there.</summary>
public sealed record MemberDuesElsewhereDto
{
    public required Guid KurinKey { get; init; }
    public required int? KurinNumber { get; init; }
    public required string? KurinNamedAfter { get; init; }
    /// <summary>The person's own line in that kurin: УПС for a сеніор in a kurin of both senior гілки.</summary>
    public required KurinBranch Branch { get; init; }
    public required IReadOnlyList<KurinNumbering> Numbering { get; init; }
    public required string Currency { get; init; }
    public required string NumberLocale { get; init; }
    /// <summary>Negative is debt, positive a surplus.</summary>
    public required decimal Balance { get; init; }
}
