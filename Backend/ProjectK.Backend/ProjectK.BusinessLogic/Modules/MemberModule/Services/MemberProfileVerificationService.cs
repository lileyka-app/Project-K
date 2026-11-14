using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.MemberModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.Shared;
using MemberEntity = ProjectK.Common.Entities.MemberModule.Member;
using MembershipEntity = ProjectK.Common.Entities.MemberModule.Membership;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

public sealed class MemberProfileVerificationService : IMemberProfileVerificationService
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDomainEventPublisher _events;
    private readonly IBlobReadLinks _links;
    private readonly IResourceScopeRepository _scopeReader;
    private readonly IUnitOfWork _kurinData;
    private readonly IMembershipRepository _memberships;
    private readonly IMemberFieldAccess _fieldAccess;
    private readonly IKurinAudit _audit;

    public MemberProfileVerificationService(
        IMemberUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IBlobReadLinks links,
        IUnitOfWork kurinData,
        IMemberFieldAccess fieldAccess,
        IKurinAudit audit)
    {
        _fieldAccess = fieldAccess;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _events = events;
        _links = links;
        _scopeReader = kurinData.ResourceScopes;
        _kurinData = kurinData;
        _memberships = kurinData.Memberships;
    }

    public async Task<ServiceResult<MemberDto>> VerifyAsync(
        Guid memberKey,
        string? note,
        CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(memberKey, cancellationToken);
        var (validation, groupKey) = await ValidateAsync(member, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var statusBefore = member!.ProfileVerificationStatus;
        member.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedCurrent;
        member.ProfileVerifiedAtUtc = DateTime.UtcNow;
        member.ProfileVerifiedByUserKey = _currentUserContext.UserId;
        member.ProfileVerificationNote = TextRules.TrimOrNull(note);
        member.UpdatedDate = DateTime.UtcNow;

        // Checking a profile again is an act of its own, so it is recorded even when it was already current.
        await RecordAsync(AuditActionNames.Member.ProfileVerified, member, statusBefore, member.ProfileVerificationNote, cancellationToken);

        var result = await SaveAsync(member, groupKey, cancellationToken);
        if (result.Type == ResultType.Success)
        {
            await PublishProfileVerifiedAsync(member, cancellationToken);
        }

        return result;
    }

    public async Task<ServiceResult<MemberDto>> ResetAsync(
        Guid memberKey,
        CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(memberKey, cancellationToken);
        var (validation, groupKey) = await ValidateAsync(member, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var statusBefore = member!.ProfileVerificationStatus;
        member.ProfileVerificationStatus = MemberProfileVerificationStatus.Unverified;
        member.ProfileVerifiedAtUtc = null;
        member.ProfileVerifiedByUserKey = null;
        member.ProfileVerificationNote = null;
        member.UpdatedDate = DateTime.UtcNow;

        if (statusBefore != MemberProfileVerificationStatus.Unverified)
        {
            await RecordAsync(AuditActionNames.Member.VerificationReset, member, statusBefore, null, cancellationToken);
        }

        return await SaveAsync(member, groupKey, cancellationToken);
    }

    /// <summary>Refuses what may not be done, or answers with the гурток the person is placed in.</summary>
    private async Task<(ServiceResult<MemberDto>? Failure, Guid? GroupKey)> ValidateAsync(MemberEntity? member, CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return (ServiceResult<MemberDto>.Failure(ResultType.Unauthorized, MemberErrors.Unauthorized, "The access token carries no readable user identity."), null);
        }

        if (member is null)
        {
            return (ServiceResult<MemberDto>.Failure(ResultType.NotFound, MemberErrors.MemberNotFound, "No such member."), null);
        }

        // Whether profiles are checked at all is the asking kurin's own setting, and it is that
        // kurin's membership that puts this person in reach — a person the caller cannot reach
        // is refused below in any case.
        var membership = await _memberships.GetActiveForMemberAsync(member.MemberKey, cancellationToken);
        var here = membership.FirstOrDefault(m => m.KurinKey == _currentUserContext.KurinKey)
            ?? membership.FirstOrDefault();

        if (here is null)
        {
            return (ServiceResult<MemberDto>.Failure(
                ResultType.BadRequest,
                MemberErrors.MemberBelongsNowhere,
                "This person does not currently belong to a kurin."), null);
        }

        var kurin = await _kurinData.Kurins.GetByKeyAsync(here.KurinKey, cancellationToken);
        if (kurin is null || !kurin.ProfileVerificationEnabled)
        {
            return (ServiceResult<MemberDto>.Failure(
                ResultType.BadRequest,
                MemberErrors.ProfileVerificationDisabled,
                "Profile verification is disabled for this kurin."), null);
        }

        if (member.UserKey.HasValue && member.UserKey.Value == _currentUserContext.UserId.Value)
        {
            return (ServiceResult<MemberDto>.Failure(ResultType.Forbidden, MemberErrors.ProfileVerificationNotAllowed, "Nobody verifies their own profile."), null);
        }

        if (await CanVerifyAsync(here, cancellationToken))
        {
            return (null, here.GroupKey);
        }

        return (ServiceResult<MemberDto>.Failure(ResultType.Forbidden, MemberErrors.ProfileVerificationNotAllowed, "You may not verify this member's profile."), null);
    }

    private async Task<bool> CanVerifyAsync(MembershipEntity here, CancellationToken cancellationToken)
    {
        if (_currentUserContext.IsAdmin())
        {
            return true;
        }

        if (_currentUserContext.CanManageWholeKurin())
        {
            return _currentUserContext.KurinKey.HasValue
                   && _currentUserContext.KurinKey.Value == here.KurinKey;
        }

        if (!_currentUserContext.CanLeadGroups()
            || !here.GroupKey.HasValue
            || !_currentUserContext.UserId.HasValue
            || !_currentUserContext.KurinKey.HasValue)
        {
            return false;
        }

        var ledGroups = await _scopeReader.GetLedGroupKeysAsync(
            _currentUserContext.UserId.Value,
            _currentUserContext.KurinKey.Value,
            cancellationToken);

        return ledGroups.Contains(here.GroupKey.Value);
    }

    private async Task<ServiceResult<MemberDto>> SaveAsync(MemberEntity member, Guid? groupKey, CancellationToken cancellationToken)
    {
        _unitOfWork.Members.Update(member, cancellationToken);
        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return ServiceResult<MemberDto>.Failure(ResultType.InternalServerError, MemberErrors.MemberNotSaved, "The verification could not be saved.");
        }

        var mask = await _fieldAccess.ForViewerAsync(cancellationToken);
        return new ServiceResult<MemberDto>(ResultType.Success, member.ToDto(_links).MaskFor(mask, groupKey));
    }

    private Task RecordAsync(
        string action,
        MemberEntity member,
        MemberProfileVerificationStatus statusBefore,
        string? note,
        CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Member,
                action,
                member.MemberKey,
                MemberTrail.LabelOf(member),
                member.MemberKey,
                AuditChangeRules.Of((MemberTrail.ProfileVerification, statusBefore, member.ProfileVerificationStatus)),
                note),
            cancellationToken);

    private async Task PublishProfileVerifiedAsync(MemberEntity member, CancellationToken cancellationToken)
    {
        if (!member.UserKey.HasValue)
        {
            return;
        }

        await _events.PublishAsync(
            new MemberProfileVerified(member.MemberKey, member.UserKey.Value, _currentUserContext.UserId),
            cancellationToken);
    }
}
