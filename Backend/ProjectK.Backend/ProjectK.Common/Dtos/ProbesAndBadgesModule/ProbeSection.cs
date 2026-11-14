namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

public sealed record ProbeSection(string Id, string Code, string Title, IReadOnlyList<ProbePoint> Points);
