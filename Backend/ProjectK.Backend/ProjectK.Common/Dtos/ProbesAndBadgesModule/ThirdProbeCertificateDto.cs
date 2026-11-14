namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

/// <summary>The camp посвідка attached to the third probe.</summary>
/// <param name="Url">A signed link that expires.</param>
public sealed record ThirdProbeCertificateDto(string Url, string ContentType, string? FileName);
