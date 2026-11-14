using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Records.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// Reads a kurin's policy from its record, cached for a minute under
/// <see cref="BackendCachePolicies.KurinAccessPolicyReads"/>. A document it cannot read gives the
/// defaults and a warning in the log, never a failed request.
/// </summary>
public sealed class KurinAccessPolicyReader : IKurinAccessPolicyReader
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackendCache _cache;
    private readonly ILogger<KurinAccessPolicyReader> _logger;

    public KurinAccessPolicyReader(IUnitOfWork unitOfWork, IBackendCache cache, ILogger<KurinAccessPolicyReader> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public Task<KurinAccessPolicy> GetAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        return _cache.GetOrCreateAsync(
            BackendCachePolicies.KurinAccessPolicyReads,
            kurinKey.ToString(),
            token => ReadAsync(kurinKey, token),
            cancellationToken);
    }

    public void Invalidate()
    {
        _cache.Invalidate(BackendCachePolicies.KurinAccessPolicyReads);
    }

    private async Task<KurinAccessPolicy> ReadAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken);
        return KurinAccessPolicy.Read(
            kurin?.AccessPolicy,
            problem => _logger.LogWarning("Access policy of kurin {KurinKey}: {Problem}", kurinKey, problem));
    }
}
