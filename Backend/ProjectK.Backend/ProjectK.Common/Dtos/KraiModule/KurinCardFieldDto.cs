namespace ProjectK.Common.Dtos.KraiModule;

/// <summary>One field a kurin may leave off its card, and whether it does.</summary>
public sealed record KurinCardFieldDto(string Key, string Label, bool Hidden);
