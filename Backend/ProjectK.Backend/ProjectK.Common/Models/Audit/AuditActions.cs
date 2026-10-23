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
}
