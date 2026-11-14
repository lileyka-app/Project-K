using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Dtos.MeModule;

/// <summary>The kurin a row belongs to — shown only when the person stands in more than one.</summary>
public sealed record MyKurinRefDto
{
    public required Guid KurinKey { get; init; }
    public required int? KurinNumber { get; init; }
    /// <summary>Every гілка with the kurin's number there, main one first.</summary>
    public required IReadOnlyList<KurinNumbering> Branches { get; init; }
    public required string? NamedAfter { get; init; }
    /// <summary>Whether this is the kurin the token acts in; what can be done from the dashboard depends on it.</summary>
    public required bool IsCurrent { get; init; }
}
