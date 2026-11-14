using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Records.AuthModule;

namespace ProjectK.API.Tests.TestHelpers;

/// <summary>A policy provider holding whatever policy the test last set — the defaults unless told.</summary>
public sealed class FixedKurinAccessPolicyProvider : IKurinAccessPolicyReader
{
    public KurinAccessPolicy Policy { get; set; } = KurinAccessPolicy.Default;

    public Task<KurinAccessPolicy> GetAsync(Guid kurinKey, CancellationToken cancellationToken = default) => Task.FromResult(Policy);

    public void Invalidate()
    {
    }
}
