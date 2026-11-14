namespace ProjectK.Common.Models.Dtos.KraiModule;

/// <summary>A край as a form offers it.</summary>
/// <param name="IsDefault">The край a kurin opens in when nobody picks one (<c>Krai:Default</c>).</param>
public sealed record KraiNameDto(string Code, string Name, bool IsDefault);
