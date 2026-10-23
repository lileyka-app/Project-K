using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;

namespace ProjectK.API.Tests.TestHelpers;

/// <summary>A policy provider holding whatever policy the test last set — the defaults unless told.</summary>
public sealed class FixedKurinAccessPolicyProvider : IKurinAccessPolicyProvider
{
    public KurinAccessPolicy Policy { get; set; } = KurinAccessPolicy.Default;

    public Task<KurinAccessPolicy> GetAsync(Guid kurinKey, CancellationToken cancellationToken = default) => Task.FromResult(Policy);

    public void Invalidate()
    {
    }
}
