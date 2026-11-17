namespace ProjectK.Common.Records.DuesModule;

/// <summary>
/// The kurin's own box and how each гурток stands with it. <see cref="DuesCashBox.ToForward"/> is
/// <see cref="ToStanytsia"/> and <see cref="ToKrai"/> together.
/// </summary>
public sealed record KurinDuesBox(DuesCashBox Box, IReadOnlyList<GroupHandover> Handovers, decimal ToStanytsia = 0, decimal ToKrai = 0);
