using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Rules.KurinModule;

/// <summary>
/// Where the line between юнацтво and кадра виховників runs, said once so the реєстр and the
/// звіт куреня cannot answer it differently.
/// <para>
/// <see cref="ProjectK.Common.Entities.KurinModule.Membership"/> has a <c>Kind</c> field meant
/// for exactly this, and it is <c>Youth</c> on every row that exists: the migration that added
/// it deliberately did not guess, and nothing has set it since. Until a провід starts filling
/// it in, the question is answered by the offices instead — and when they do, <c>Kind</c>
/// should win here and this become the fallback.
/// </para>
/// </summary>
public static class KurinRosterRules
{
    /// <summary>
    /// Whether holding this office puts a person in the кадра of <paramref name="kurinKey"/>.
    /// <para>
    /// Only <see cref="LeadershipType.KV"/> counts. A гуртковий holds a <c>Group</c> office and
    /// a курінний a <c>Kurin</c> one, and both are themselves юнаки — splitting on "holds an
    /// office" would take the people who run the гуртки out of the юнаки table.
    /// </para>
    /// <param name="heldUntil">When the person's own term ended; <c>null</c> means they hold it.</param>
    /// <param name="officeEnd">When the office itself was closed; <c>null</c> means it is open.</param>
    /// </summary>
    public static bool IsStaffOffice(
        LeadershipType type,
        Guid? officeKurinKey,
        DateOnly? officeEnd,
        DateOnly? heldUntil,
        Guid kurinKey)
        => type == LeadershipType.KV
           && officeKurinKey == kurinKey
           && officeEnd is null
           && heldUntil is null;

    /// <summary>
    /// Whether a закріплення за гуртком puts a person in the кадра of <paramref name="kurinKey"/>:
    /// it is still in force and its гурток is this kurin's. Access already reads an assignment as
    /// the впорядник's office (<c>MentorOfficeRules</c>), and the sidebar calls the person that;
    /// the реєстр and the звіт have to say the same, or the report prints among the юнаки someone
    /// the screen next to it lists as кадра.
    /// </summary>
    public static bool IsStaffAssignment(DateTime? revokedAtUtc, Guid? assignmentKurinKey, Guid kurinKey)
        => revokedAtUtc is null && assignmentKurinKey == kurinKey;
}
