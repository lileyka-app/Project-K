using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <summary>The limits as numbers, so a client says the same thing the server enforces.</summary>
public sealed record ProbeProjectLimitsDto(
    int MaxMainProjects,
    int MaxSpareProjects,
    int MaxSharedMainProjects,
    int MaxImagesPerProject,
    long MaxImageBytes,
    long MaxCertificateBytes,
    long MaxStoredBytes,
    int MaxTextLength,
    int RequiredBadges,
    int YouthEndsAtAge);
