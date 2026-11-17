namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>A tag of the kurin.</summary>
/// <param name="KurinTagKey"></param>
/// <param name="Name"></param>
/// <param name="ColorHex"></param>
/// <param name="MemberCount">How many people carry it; null for whoever may not manage tags.</param>
public sealed record KurinTagDto(Guid KurinTagKey, string Name, string ColorHex, int? MemberCount);
