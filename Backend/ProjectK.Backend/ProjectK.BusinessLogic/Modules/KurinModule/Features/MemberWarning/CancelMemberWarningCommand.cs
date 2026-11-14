using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Reports;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberWarning;

public sealed class CancelMemberWarningCommand : IRequest<ServiceResult<MemberWarningDto>>
{
    public CancelMemberWarningCommand(Guid memberKey, Guid warningKey)
    {
        MemberKey = memberKey;
        WarningKey = warningKey;
    }

    public Guid MemberKey { get; }
    public Guid WarningKey { get; }
}

public sealed class CancelMemberWarningCommandHandler : IRequestHandler<CancelMemberWarningCommand, ServiceResult<MemberWarningDto>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly TimeProvider _timeProvider;
    private readonly IResourceAccessService _access;
    private readonly IKurinAudit _audit;

    public CancelMemberWarningCommandHandler(
        IMemberUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        TimeProvider timeProvider,
        IResourceAccessService access,
        IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _timeProvider = timeProvider;
        _access = access;
        _audit = audit;
    }

    public async Task<ServiceResult<MemberWarningDto>> Handle(CancelMemberWarningCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return ServiceResult<MemberWarningDto>.Failure(ResultType.Unauthorized, MemberErrors.Unauthorized, "The access token carries no readable user identity.");
        }

        var warning = await _unitOfWork.MemberWarnings.GetByKeyAsync(request.WarningKey, cancellationToken);
        if (warning is null || warning.MemberKey != request.MemberKey)
        {
            return ServiceResult<MemberWarningDto>.Failure(ResultType.NotFound, MemberErrors.WarningNotFound, "No such warning on this member.");
        }

        if (warning.RevokedAtUtc.HasValue || warning.ExpiresAtUtc <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            return ServiceResult<MemberWarningDto>.Failure(ResultType.Conflict, MemberErrors.WarningNotActive, "The warning is already cancelled or has expired.");
        }

        if (!await MayCancelAsync(warning, _currentUserContext.UserId.Value, cancellationToken))
        {
            return ServiceResult<MemberWarningDto>.Failure(ResultType.Forbidden, MemberErrors.WarningCancelNotAllowed, "You may not cancel this warning.");
        }

        warning.RevokedAtUtc = DateTime.UtcNow;
        warning.RevokedByUserKey = _currentUserContext.UserId.Value;
        warning.UpdatedDate = warning.RevokedAtUtc.Value;

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Warning,
                AuditActions.Warning.Cancelled,
                warning.MemberWarningKey,
                KurinReportTerminology.WarningLevel(warning.Level),
                warning.MemberKey,
                AuditChanges.Removed(
                    ("level", warning.Level),
                    ("issuedAtUtc", warning.IssuedAtUtc),
                    ("expiresAtUtc", warning.ExpiresAtUtc))),
            cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return ServiceResult<MemberWarningDto>.Failure(ResultType.InternalServerError, MemberErrors.MemberNotSaved, "The cancellation could not be saved.");
        }

        var response = warning.ToDto();
        return new ServiceResult<MemberWarningDto>(ResultType.Success, response);
    }

    /// <summary>
    /// Whoever gave a пересторога may take it back while they could still give it; anyone else
    /// needs <see cref="KurinCapability.CancelWarning"/>.
    /// </summary>
    private async Task<bool> MayCancelAsync(
        Common.Entities.KurinModule.MemberWarning warning,
        Guid userKey,
        CancellationToken cancellationToken)
    {
        if (warning.IssuedByUserKey == userKey
            && (await _access.CheckCapabilityAsync(
                KurinCapabilities.GiveWarning(warning.Level), ResourceType.Member, warning.MemberKey, cancellationToken)).IsAllowed)
        {
            return true;
        }

        return (await _access.CheckCapabilityAsync(
            KurinCapability.CancelWarning, ResourceType.Member, warning.MemberKey, cancellationToken)).IsAllowed;
    }
}

