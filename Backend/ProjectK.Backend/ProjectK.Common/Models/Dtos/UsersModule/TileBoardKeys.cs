namespace ProjectK.Common.Models.Dtos.UsersModule;

public static class TileBoardKeys
{
    public const string MemberCard = "member-card";
    public const string KurinPanel = "kurin-panel";
    public const string GroupPanel = "group-panel";
    public const string Dashboard = "dashboard";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        MemberCard,
        KurinPanel,
        GroupPanel,
        Dashboard
    };
}
