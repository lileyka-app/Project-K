namespace ProjectK.Common.Options.InfrastructureModule;

public sealed class OrphanCleanupOptions
{
    // Enable/disable the cleanup service.
    public bool Enabled { get; init; } = true;

    // Interval between full runs (default 6 hours).
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(6);

    // Minimum "age" of a blob before deletion (to avoid deleting just-uploaded files).
    public TimeSpan GracePeriod { get; init; } = TimeSpan.FromHours(1);

    // Limit of deletions per run (to prevent mass deletions).
    public int MaxDeletesPerRun { get; init; } = 500;

    // If true, only log actions without actual deletions.
    public bool DryRun { get; init; } = false;

    // Additional random delay (jitter) in seconds (0..JitterSeconds).
    public int JitterSeconds { get; init; } = 30;
}
