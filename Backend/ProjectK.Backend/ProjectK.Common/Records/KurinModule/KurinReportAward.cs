using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportAward(
    MemberAwardLevel? Level,
    string LevelLabel,
    DateTime DateAcquired,
    string? Note,
    BadgeProgressStatus Status,
    string StatusLabel);
