namespace ProjectK.Common.Dtos.MemberModule;

/// <param name="Sign">Sign points, complete and verify.</param>
/// <param name="Unsign">Remove a signature, take a проба back from verified.</param>
public sealed record ProbeAbilitiesDto(bool Sign, bool Unsign);
