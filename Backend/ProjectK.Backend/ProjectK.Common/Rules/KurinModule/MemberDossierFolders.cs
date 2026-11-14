namespace ProjectK.Common.Rules.KurinModule;

/// <summary>
/// The names a dossier's folders go by. They appear in routes and in the <c>include</c> parameter, so
/// they are written once here rather than spelled out at every call site.
/// </summary>
public static class MemberDossierFolders
{
    public const string Memberships = "memberships";
    public const string Offices = "offices";
    public const string Levels = "levels";
    public const string Awards = "awards";
    public const string Warnings = "warnings";
    public const string Progress = "progress";

    public static readonly IReadOnlyCollection<string> All =
        [Memberships, Offices, Levels, Awards, Warnings, Progress];

    public static bool Exists(string name) =>
        All.Any(folder => string.Equals(folder, name, StringComparison.OrdinalIgnoreCase));
}
