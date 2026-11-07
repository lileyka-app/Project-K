namespace ProjectK.Common.Models.Audit;

/// <summary>
/// Every action the kurin's journal records, as the string it is stored under. The strings are data:
/// one already written stays as it is, and the clients name an entry by it.
/// </summary>
public static class AuditActions
{
    /// <summary>Проби: their points and their status.</summary>
    public static class Probe
    {
        public const string PointSigned = "Probe.PointSigned";
        public const string PointUnsigned = "Probe.PointUnsigned";

        /// <summary>A point was unsigned on a проба that was already signed off, which sends it back to work.</summary>
        public const string Reopened = "Probe.Reopened";

        public const string Started = "Probe.Started";
        public const string Completed = "Probe.Completed";
        public const string Verified = "Probe.Verified";
        public const string Unverified = "Probe.Unverified";

        /// <summary>A project of the third probe was described, rewritten or removed.</summary>
        public const string ProjectAdded = "Probe.ProjectAdded";
        public const string ProjectEdited = "Probe.ProjectEdited";
        public const string ProjectRemoved = "Probe.ProjectRemoved";
        public const string ProjectApproved = "Probe.ProjectApproved";
        public const string ProjectApprovalRemoved = "Probe.ProjectApprovalRemoved";
        public const string ProjectCompleted = "Probe.ProjectCompleted";
        public const string ProjectCompletionRemoved = "Probe.ProjectCompletionRemoved";

        /// <summary>The spare project took a main one's place.</summary>
        public const string ProjectsSwapped = "Probe.ProjectsSwapped";

        /// <summary>The third-probe file: mentors, reflection, camp, résumé.</summary>
        public const string FileEdited = "Probe.FileEdited";
        public const string ReflectionConfirmed = "Probe.ReflectionConfirmed";
        public const string ReflectionUnconfirmed = "Probe.ReflectionUnconfirmed";

        /// <summary>The referent's stages: motivation letter, self-reflection, application, opened, done, named, archived.</summary>
        public const string StagesChanged = "Probe.StagesChanged";
        public const string CertificateAttached = "Probe.CertificateAttached";
        public const string CertificateRemoved = "Probe.CertificateRemoved";

        /// <summary>The action of confirming (or taking back) a part of the third probe, by the part's name.</summary>
        public static string ThirdProbe(string part, bool confirmed) => (part, confirmed) switch
        {
            ("Reflection", true) => ReflectionConfirmed,
            ("Reflection", false) => ReflectionUnconfirmed,
            ("ProjectApproval", true) => ProjectApproved,
            ("ProjectApproval", false) => ProjectApprovalRemoved,
            ("ProjectCompletion", true) => ProjectCompleted,
            ("ProjectCompletion", false) => ProjectCompletionRemoved,
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Unknown part of the third probe.")
        };
    }

    /// <summary>Вмілості.</summary>
    public static class Badge
    {
        public const string Submitted = "Badge.Submitted";
        public const string Resubmitted = "Badge.Resubmitted";
        public const string Confirmed = "Badge.Confirmed";
        public const string Rejected = "Badge.Rejected";
        public const string ConfirmationRemoved = "Badge.ConfirmationRemoved";
    }

    /// <summary>Відзначення.</summary>
    public static class Award
    {
        public const string Added = "Award.Added";
        public const string Changed = "Award.Changed";
        public const string Confirmed = "Award.Confirmed";
        public const string Rejected = "Award.Rejected";
        public const string Deleted = "Award.Deleted";
    }

    /// <summary>Перестороги.</summary>
    public static class Warning
    {
        public const string Given = "Warning.Given";
        public const string Cancelled = "Warning.Cancelled";
    }

    /// <summary>Події й задачі. Answers to an invitation are not recorded: there are too many of them to read.</summary>
    public static class Agenda
    {
        public const string Created = "Agenda.Created";
        public const string Changed = "Agenda.Changed";
        public const string Deleted = "Agenda.Deleted";

        /// <summary>A task, one of its targets or one person's part of it moved to another column.</summary>
        public const string StatusChanged = "Agenda.StatusChanged";

        public const string Archived = "Agenda.Archived";
        public const string Restored = "Agenda.Restored";

        /// <summary>One occurrence of an event was said not to have taken place — no сходини that week.</summary>
        public const string OccurrenceNotHeld = "Agenda.OccurrenceNotHeld";

        /// <summary>That was taken back, by hand or by marking someone present at it.</summary>
        public const string OccurrenceHeld = "Agenda.OccurrenceHeld";
    }

    /// <summary>A person's card.</summary>
    public static class Member
    {
        public const string Added = "Member.Added";
        public const string Changed = "Member.Changed";
        public const string PhotoChanged = "Member.PhotoChanged";
        public const string PhotoRemoved = "Member.PhotoRemoved";
        public const string Deleted = "Member.Deleted";
        public const string ProfileVerified = "Member.ProfileVerified";
        public const string VerificationReset = "Member.VerificationReset";
    }

    /// <summary>Ролі проводу and впорядники of гуртки.</summary>
    public static class Office
    {
        /// <summary>Someone took the role, in place of whoever held it or into an empty one.</summary>
        public const string Assigned = "Office.Assigned";

        /// <summary>The role was left empty.</summary>
        public const string Ended = "Office.Ended";

        public const string MentorAssigned = "Office.MentorAssigned";
        public const string MentorRevoked = "Office.MentorRevoked";

        /// <summary>A former holding's dates or role were corrected in the archive.</summary>
        public const string Corrected = "Office.Corrected";
    }

    /// <summary>Who belongs to the kurin, and in which гурток.</summary>
    public static class Membership
    {
        public const string Joined = "Membership.Joined";
        public const string Left = "Membership.Left";
        public const string MovedToGroup = "Membership.MovedToGroup";

        /// <summary>A former membership's dates or kind were corrected in the archive.</summary>
        public const string Corrected = "Membership.Corrected";
    }

    /// <summary>
    /// The kurin's archive and its danger zone. A deletion carries what was deleted in its changes:
    /// the record itself is gone, so the entry is the only trace left of it.
    /// </summary>
    public static class Archive
    {
        public const string GroupDissolved = "Archive.GroupDissolved";
        public const string DangerZoneEntered = "Archive.DangerZoneEntered";

        /// <summary>A wrong password or code; the entry carries which, never what was typed.</summary>
        public const string DangerZoneRefused = "Archive.DangerZoneRefused";

        public const string DangerZoneEnded = "Archive.DangerZoneEnded";
        public const string OfficeDeleted = "Archive.OfficeDeleted";
        public const string MembershipDeleted = "Archive.MembershipDeleted";
        public const string GroupDeleted = "Archive.GroupDeleted";
    }

    /// <summary>The kurin's own settings.</summary>
    public static class Settings
    {
        /// <summary>The kurin's details: number, станиця, profile verification and the like.</summary>
        public const string KurinChanged = "Settings.KurinChanged";

        /// <summary>«Права й видимість»: one change per cell, named <c>Capability/Audience</c>.</summary>
        public const string AccessPolicyChanged = "Settings.AccessPolicyChanged";

        public const string CardFieldsChanged = "Settings.CardFieldsChanged";
        public const string ArchivePolicyChanged = "Settings.ArchivePolicyChanged";

        /// <summary>A гурток moved to another edition of the probe programme.</summary>
        public const string GroupProbeEditionChanged = "Settings.GroupProbeEditionChanged";

        /// <summary>Tags turned on or off, or whether people see their own.</summary>
        public const string TagSettingsChanged = "Settings.TagSettingsChanged";
    }

    /// <summary>The kurin's tags and who carries them.</summary>
    public static class Tag
    {
        public const string Created = "Tag.Created";
        public const string Changed = "Tag.Changed";

        /// <summary>The tag is gone, and with it from everyone who carried it.</summary>
        public const string Deleted = "Tag.Deleted";

        /// <summary>The tag was put on a person; one entry per person.</summary>
        public const string Put = "Tag.Put";

        /// <summary>The tag was taken off a person.</summary>
        public const string TakenOff = "Tag.TakenOff";
    }
}
