using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>A відзначення as the report reads it, in whatever state it stands.</summary>
public sealed record KurinReportSourceAward(
    MemberAwardLevel? Level,
    string? CatalogId,
    DateTime DateAcquired,
    string? Note,
    BadgeProgressStatus Status);
