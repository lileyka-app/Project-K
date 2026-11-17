using ProjectK.Common.Dtos.KurinModule;

namespace ProjectK.Common.Dtos.MemberModule;

public sealed record PublicCardKurinDto(int? Number, string? NamedAfter, string? GroupName, IReadOnlyList<KurinNumbering> Branches);
