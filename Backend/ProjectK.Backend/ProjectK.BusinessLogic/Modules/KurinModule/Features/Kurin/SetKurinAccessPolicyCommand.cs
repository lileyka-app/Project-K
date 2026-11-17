using MediatR;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

/// <summary>
/// Saves the kurin's policy from the whole effective matrix (capability → audience → reach, by
/// name). Only what differs from the defaults is stored; a matrix that is not valid is refused.
/// </summary>
public sealed record SetKurinAccessPolicyCommand(
    Guid KurinKey,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Grants) : IRequest<ServiceResult<KurinAccessPolicyDto>>;

public sealed class SetKurinAccessPolicyCommandHandler : IRequestHandler<SetKurinAccessPolicyCommand, ServiceResult<KurinAccessPolicyDto>>
{
    /// <summary>What the kurin's journal calls this setting.</summary>
    public const string Label = "Права й видимість";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IKurinAccessPolicyReader _policies;
    private readonly IAccessChangeStore _accessChanges;
    private readonly IKurinAudit _audit;

    public SetKurinAccessPolicyCommandHandler(
        IUnitOfWork unitOfWork,
        IKurinAccessPolicyReader policies,
        IAccessChangeStore accessChanges,
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
            return KurinErrors.NotFound<KurinAccessPolicyDto>();
        }

        var before = KurinAccessPolicy.Read(kurin.AccessPolicy);
        var policy = KurinAccessPolicy.FromMatrix(request.Grants);
        kurin.AccessPolicy = policy.Write();
        _unitOfWork.Kurins.Update(kurin, cancellationToken);

        var changes = ChangedCells(before, policy);
        if (changes.Count > 0)
        {
            await _audit.RecordAsync(
                new AuditRecord(AuditArea.Settings, AuditActionNames.Settings.AccessPolicyChanged, kurin.KurinKey, Label, Changes: changes, KurinKey: kurin.KurinKey),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _policies.Invalidate();
        _accessChanges.MarkKurin(request.KurinKey);

        return new ServiceResult<KurinAccessPolicyDto>(ResultType.Success, policy.ToDto());
    }

    /// <summary>Every cell whose reach differs, named <c>Capability/Audience</c>, from the old reach to the new.</summary>
    private static IReadOnlyList<AuditChange> ChangedCells(KurinAccessPolicy before, KurinAccessPolicy after) =>
        AuditChangeRules.Of(KurinCapabilityRules.All
            .SelectMany(descriptor => KurinCapabilityRules.Audiences.Select(audience => (
                Field: $"{descriptor.Key}/{audience}",
                From: (object?)before.Reach(descriptor.Key, audience),
                To: (object?)after.Reach(descriptor.Key, audience))))
            .ToArray());
}
