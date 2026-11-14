using ProjectK.Common.Records.KurinModule;

namespace ProjectK.Common.Dtos.MeModule;

public sealed record PublicCardKurinDto(int? Number, string? NamedAfter, string? GroupName, IReadOnlyList<KurinNumbering> Branches);
