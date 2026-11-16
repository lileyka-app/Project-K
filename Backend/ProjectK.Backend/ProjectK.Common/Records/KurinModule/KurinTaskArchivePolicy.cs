namespace ProjectK.Common.Records.KurinModule;

/// <summary>A kurin's rules for its done tasks, in days; an empty period switches that step off.</summary>
public sealed record KurinTaskArchivePolicy(int? AutoArchiveAfterDays, int? PurgeAfterDays);
