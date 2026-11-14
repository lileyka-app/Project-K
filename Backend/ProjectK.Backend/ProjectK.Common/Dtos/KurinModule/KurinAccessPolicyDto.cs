using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>A kurin's «Права й видимість» as its settings page shows it.</summary>
/// <param name="Audiences">The columns, in order.</param>
/// <param name="Capabilities">The rows, in order.</param>
public sealed record KurinAccessPolicyDto(
    IReadOnlyList<PolicyAudience> Audiences,
    IReadOnlyList<KurinCapabilityDto> Capabilities);
