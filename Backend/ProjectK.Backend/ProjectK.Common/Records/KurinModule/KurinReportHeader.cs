namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportHeader(
    DateTime GeneratedAtUtc,
    string GeneratedByName,
    string? GeneratedByEmail,
    string BackendVersion,
    // Null for a release published without a code name; the footer then prints the version alone.
    string? BackendCodename);
