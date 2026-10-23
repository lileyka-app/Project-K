using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.AccessPolicy;

/// <summary>The kurin's «Права й видимість», every cell with its default and lock.</summary>
public sealed record GetKurinAccessPolicyQuery(Guid KurinKey) : IRequest<ServiceResult<KurinAccessPolicyDto>>;

/// <summary>
/// Saves the kurin's policy from the whole effective matrix (capability → audience → reach, by
/// name). Only what differs from the defaults is stored; a matrix that is not valid is refused.
/// </summary>
public sealed record SetKurinAccessPolicyCommand(
    Guid KurinKey,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Grants) : IRequest<ServiceResult<KurinAccessPolicyDto>>;

public sealed class GetKurinAccessPolicyQueryHandler : IRequestHandler<GetKurinAccessPolicyQuery, ServiceResult<KurinAccessPolicyDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKurinAccessPolicyProvider _policies;

    public GetKurinAccessPolicyQueryHandler(IUnitOfWork unitOfWork, IKurinAccessPolicyProvider policies)
    {
        _unitOfWork = unitOfWork;
        _policies = policies;
    }

    public async Task<ServiceResult<KurinAccessPolicyDto>> Handle(GetKurinAccessPolicyQuery request, CancellationToken cancellationToken)
    {
        if (await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken) is null)
        {
            return KurinAccessPolicyResult.NotFound();
        }

        return KurinAccessPolicyResult.Of(await _policies.GetAsync(request.KurinKey, cancellationToken));
    }
}

public sealed class SetKurinAccessPolicyCommandHandler : IRequestHandler<SetKurinAccessPolicyCommand, ServiceResult<KurinAccessPolicyDto>>
{
    /// <summary>What the kurin's journal calls this setting.</summary>
    public const string Label = "Права й видимість";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IKurinAccessPolicyProvider _policies;
    private readonly IAccessChangeSignal _accessChanges;
    private readonly IKurinAudit _audit;

    public SetKurinAccessPolicyCommandHandler(
        IUnitOfWork unitOfWork,
        IKurinAccessPolicyProvider policies,
        IAccessChangeSignal accessChanges,
        IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _policies = policies;
        _accessChanges = accessChanges;
        _audit = audit;
    }

    public async Task<ServiceResult<KurinAccessPolicyDto>> Handle(SetKurinAccessPolicyCommand request, CancellationToken cancellationToken)
    {
        var errors = KurinAccessPolicy.Validate(request.Grants);
        if (errors.Count > 0)
        {
            return ServiceResult<KurinAccessPolicyDto>.Failure(
                ResultType.BadRequest,
                KurinAccessPolicy.InvalidErrorCode,
                string.Join(" ", errors));
        }

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return KurinAccessPolicyResult.NotFound();
        }

        var before = KurinAccessPolicy.Read(kurin.AccessPolicy);
        var policy = KurinAccessPolicy.FromMatrix(request.Grants);
        kurin.AccessPolicy = policy.Write();
        _unitOfWork.Kurins.Update(kurin, cancellationToken);

        var changes = ChangedCells(before, policy);
        if (changes.Count > 0)
        {
            await _audit.RecordAsync(
                new AuditRecord(AuditArea.Settings, AuditActions.Settings.AccessPolicyChanged, kurin.KurinKey, Label, Changes: changes, KurinKey: kurin.KurinKey),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _policies.Invalidate();
        _accessChanges.MarkKurin(request.KurinKey);

        return KurinAccessPolicyResult.Of(policy);
    }

    /// <summary>Every cell whose reach differs, named <c>Capability/Audience</c>, from the old reach to the new.</summary>
    private static IReadOnlyList<AuditChange> ChangedCells(KurinAccessPolicy before, KurinAccessPolicy after) =>
        AuditChanges.Of(KurinCapabilities.All
            .SelectMany(descriptor => KurinCapabilities.Audiences.Select(audience => (
                Field: $"{descriptor.Key}/{audience}",
                From: (object?)before.Reach(descriptor.Key, audience),
                To: (object?)after.Reach(descriptor.Key, audience))))
            .ToArray());
}

internal static class KurinAccessPolicyResult
{
    public static ServiceResult<KurinAccessPolicyDto> Of(KurinAccessPolicy policy)
    {
        var rows = KurinCapabilities.All
            .Select(descriptor => new KurinCapabilityDto(
                descriptor.Key,
                descriptor.Group,
                descriptor.YouthConfigurable,
                descriptor.Reaches,
                KurinCapabilities.Audiences
                    .Select(audience => new AccessPolicyCellDto(
                        audience,
                        policy.Reach(descriptor.Key, audience),
                        descriptor.Defaults[audience],
                        descriptor.IsLocked(audience)))
                    .ToList()))
            .ToList();

        return new ServiceResult<KurinAccessPolicyDto>(
            ResultType.Success,
            new KurinAccessPolicyDto(KurinCapabilities.Audiences, rows));
    }

    public static ServiceResult<KurinAccessPolicyDto> NotFound()
    {
        return ServiceResult<KurinAccessPolicyDto>.Failure(ResultType.NotFound, "KurinNotFound", "Kurin not found.");
    }
}
