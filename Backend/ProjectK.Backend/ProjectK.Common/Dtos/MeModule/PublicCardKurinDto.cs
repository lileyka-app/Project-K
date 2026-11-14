using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Dtos.MeModule;

public sealed record PublicCardKurinDto(int? Number, string? NamedAfter, string? GroupName, IReadOnlyList<KurinNumbering> Branches);
