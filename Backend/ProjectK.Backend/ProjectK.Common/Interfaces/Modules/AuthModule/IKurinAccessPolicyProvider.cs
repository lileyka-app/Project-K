using ProjectK.Common.Models.Authorization.KurinPolicy;

namespace ProjectK.Common.Interfaces.Modules.AuthModule;

/// <summary>
/// The one place code asks what a kurin has set in «Права й видимість». Nothing else reads
/// <c>Kurin.AccessPolicy</c>.
/// </summary>
public interface IKurinAccessPolicyProvider
{
    /// <summary>The kurin's policy; the defaults when it has set nothing or is not there.</summary>
    Task<KurinAccessPolicy> GetAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>Drops every cached policy, so a save is seen by the next request.</summary>
    void Invalidate();
}
