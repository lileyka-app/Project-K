namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportProbePoint(
    string ProbeId,
    string ProbeTitle,
    string PointId,
    string PointLabel,
    DateTime? SignedAtUtc,
    string? SignedByName,
    string? SignedByRole);
