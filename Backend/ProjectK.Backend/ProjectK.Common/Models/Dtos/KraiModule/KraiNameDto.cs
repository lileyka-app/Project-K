namespace ProjectK.Common.Models.Dtos.KraiModule;

/// <summary>
/// A край as a form offers it. <see cref="IsDefault"/> marks the край a kurin opens in when nobody
/// picks one (<c>Krai:Default</c>).
/// </summary>
public sealed record KraiNameDto(string Code, string Name, bool IsDefault);
