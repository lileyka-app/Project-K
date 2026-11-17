namespace ProjectK.Common.Records.KurinModule;

/// <summary>A гурток by name, with the kurin it is in.</summary>
public sealed record GroupSummary(Guid GroupKey, Guid KurinKey, string Name);
