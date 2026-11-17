using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.Common.Rules.KurinModule;

public static class KurinReportNames
{
    /// <summary>
    /// The ступінь, named by <see cref="PlastLevelNames"/> — the same words the реєстр and its
    /// .xlsx use. The report used to keep its own wording; a провід reading one beside the other
    /// then had to work out that «Заприсяження» and «Прихильник» were rows of the same ladder.
    /// </summary>
    public static string PlastLevel(PlastLevel? value)
        => value is null ? "-" : PlastLevelNames.Of(value.Value);

    public static string ProbeStatus(ProbeProgressStatus value)
        => ProbeStatusLabels.GetValueOrDefault(value, value.ToString());

    public static string BadgeStatus(BadgeProgressStatus value)
        => BadgeStatusLabels.GetValueOrDefault(value, value.ToString());

    public static string WarningLevel(MemberWarningLevel value)
        => WarningLevelLabels.GetValueOrDefault(value, value.ToString());

    public static string AwardLevel(MemberAwardLevel value)
        => AwardLevelLabels.GetValueOrDefault(value, value.ToString());

    /// <summary>
    /// An УПЮ відзначення by its level, a senior one by the catalogue's title — or its id while the
    /// catalogue has not been read.
    /// </summary>
    public static string Award(MemberAwardLevel? level, string? catalogId, Func<string, string?> catalogTitle)
        => level is { } value ? AwardLevel(value) : (catalogId is null ? "-" : catalogTitle(catalogId) ?? catalogId);

    public static string LeadershipType(LeadershipType value)
        => LeadershipTypeLabels.GetValueOrDefault(value, value.ToString());

    public static string LeadershipRole(LeadershipRole value)
        => LeadershipRoleLabels.GetValueOrDefault(value, value.ToString());

    private static readonly IReadOnlyDictionary<ProbeProgressStatus, string> ProbeStatusLabels =
        new Dictionary<ProbeProgressStatus, string>
        {
            [ProjectK.Common.Enums.ProbesAndBadgesModule.ProbeProgressStatus.NotStarted] = "Не розпочато",
            [ProjectK.Common.Enums.ProbesAndBadgesModule.ProbeProgressStatus.InProgress] = "В процесі",
            [ProjectK.Common.Enums.ProbesAndBadgesModule.ProbeProgressStatus.Completed] = "Підписано",
            [ProjectK.Common.Enums.ProbesAndBadgesModule.ProbeProgressStatus.Verified] = "Підтверджено"
        };

    private static readonly IReadOnlyDictionary<BadgeProgressStatus, string> BadgeStatusLabels =
        new Dictionary<BadgeProgressStatus, string>
        {
            [ProjectK.Common.Enums.ProbesAndBadgesModule.BadgeProgressStatus.Draft] = "Чернетка",
            [ProjectK.Common.Enums.ProbesAndBadgesModule.BadgeProgressStatus.Submitted] = "Подано на підтвердження",
            [ProjectK.Common.Enums.ProbesAndBadgesModule.BadgeProgressStatus.Confirmed] = "Підтверджено",
            [ProjectK.Common.Enums.ProbesAndBadgesModule.BadgeProgressStatus.Rejected] = "Відхилено"
        };

    private static readonly IReadOnlyDictionary<MemberWarningLevel, string> WarningLevelLabels =
        new Dictionary<MemberWarningLevel, string>
        {
            [ProjectK.Common.Enums.MemberModule.MemberWarningLevel.Level1] = "1-а пересторога",
            [ProjectK.Common.Enums.MemberModule.MemberWarningLevel.Level2] = "2-а пересторога",
            [ProjectK.Common.Enums.MemberModule.MemberWarningLevel.Level3] = "3-а пересторога"
        };

    private static readonly IReadOnlyDictionary<MemberAwardLevel, string> AwardLevelLabels =
        new Dictionary<MemberAwardLevel, string>
        {
            [ProjectK.Common.Enums.MemberModule.MemberAwardLevel.First] = "1-е відзначення",
            [ProjectK.Common.Enums.MemberModule.MemberAwardLevel.Second] = "2-е відзначення",
            [ProjectK.Common.Enums.MemberModule.MemberAwardLevel.Third] = "3-е відзначення",
            [ProjectK.Common.Enums.MemberModule.MemberAwardLevel.Fourth] = "4-е відзначення"
        };

    private static readonly IReadOnlyDictionary<LeadershipType, string> LeadershipTypeLabels =
        new Dictionary<LeadershipType, string>
        {
            [ProjectK.Common.Enums.KurinModule.LeadershipType.Kurin] = "Курінь",
            [ProjectK.Common.Enums.KurinModule.LeadershipType.Group] = "Гурток",
            [ProjectK.Common.Enums.KurinModule.LeadershipType.KV] = "КВ"
        };

    private static readonly IReadOnlyDictionary<LeadershipRole, string> LeadershipRoleLabels =
        new Dictionary<LeadershipRole, string>
        {
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.Kurinnuy] = "Курінний",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.Hurtkoviy] = "Гуртковий",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.Suddya] = "Суддя",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.Pysar] = "Писар",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.Skarbnyk] = "Скарбник",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.Horunjiy] = "Хорунжий",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.Gospodar] = "Господар",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.Hronikar] = "Хронікар",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.Instruktor] = "Інструктор",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.Vykhovnyk] = "Впорядник",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.Zvyazkovyi] = "Зв'язковий",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.OtherKurin] = "Інша курінна роль",
            [ProjectK.Common.Enums.KurinModule.LeadershipRole.OtherGroup] = "Інша гурткова роль"
        };
}
