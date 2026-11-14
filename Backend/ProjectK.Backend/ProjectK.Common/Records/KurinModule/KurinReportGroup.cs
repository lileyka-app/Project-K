namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportGroup(
    Guid GroupKey,
    string Name,
    string? Description,
    string? SilhouetteUrl,
    byte[]? SilhouetteBytes,
    IReadOnlyList<string> MentorNames,
    IReadOnlyList<KurinReportGroupMember> Members);
