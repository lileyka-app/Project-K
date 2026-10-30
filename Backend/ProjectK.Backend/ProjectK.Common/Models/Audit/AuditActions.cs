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
    }

    /// <summary>Who belongs to the kurin, and in which гурток.</summary>
    public static class Membership
    {
        public const string Joined = "Membership.Joined";
        public const string Left = "Membership.Left";
        public const string MovedToGroup = "Membership.MovedToGroup";
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
    }
}
