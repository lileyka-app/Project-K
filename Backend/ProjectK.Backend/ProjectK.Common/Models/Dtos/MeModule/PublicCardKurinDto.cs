using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.MeModule;

public sealed record PublicCardKurinDto(int? Number, string? NamedAfter, string? GroupName, IReadOnlyList<KurinNumbering> Branches);
