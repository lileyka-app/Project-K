using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <param name="Url">A signed link that expires.</param>
public sealed record ThirdProbeCertificateDto(string Url, string ContentType, string? FileName);
