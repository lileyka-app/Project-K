namespace ProjectK.Common.Records.KurinModule;

/// <summary>One part of the story — «Членства», «Ролі», «Вишколи». An empty one is left out.</summary>
public sealed record LifeStorySection(string Title, IReadOnlyList<LifeStoryRow> Rows);
