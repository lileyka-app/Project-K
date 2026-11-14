namespace ProjectK.Common.Records.MemberModule;

/// <summary>A line: when, what, and whatever else names it — the kurin, the комендант, a note.</summary>
public sealed record LifeStoryRow(string When, string What, string? Detail);
