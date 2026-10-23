using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using MemberEntity = ProjectK.Common.Entities.KurinModule.Member;
using MembershipEntity = ProjectK.Common.Entities.KurinModule.Membership;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.ProfileVerification;

public sealed record VerifyMemberProfileCommand(Guid MemberKey, string? Note = null) : IRequest<ServiceResult<MemberResponse>>;

public sealed record ResetMemberProfileVerificationCommand(Guid MemberKey) : IRequest<ServiceResult<MemberResponse>>;

public sealed class VerifyMemberProfileCommandHandler : IRequestHandler<VerifyMemberProfileCommand, ServiceResult<MemberResponse>>
{
    private readonly IMemberProfileVerificationService _service;

    public VerifyMemberProfileCommandHandler(IMemberProfileVerificationService service)
    {
        _service = service;
    }

    public Task<ServiceResult<MemberResponse>> Handle(VerifyMemberProfileCommand request, CancellationToken cancellationToken)
        => _service.VerifyAsync(request.MemberKey, request.Note, cancellationToken);
}

public sealed class ResetMemberProfileVerificationCommandHandler : IRequestHandler<ResetMemberProfileVerificationCommand, ServiceResult<MemberResponse>>
{
    private readonly IMemberProfileVerificationService _service;

    public ResetMemberProfileVerificationCommandHandler(IMemberProfileVerificationService service)
    {
        _service = service;
    }

    public Task<ServiceResult<MemberResponse>> Handle(ResetMemberProfileVerificationCommand request, CancellationToken cancellationToken)
        => _service.ResetAsync(request.MemberKey, cancellationToken);
}

public interface IMemberProfileVerificationService
{
    Task<ServiceResult<MemberResponse>> VerifyAsync(Guid memberKey, string? note, CancellationToken cancellationToken);

    Task<ServiceResult<MemberResponse>> ResetAsync(Guid memberKey, CancellationToken cancellationToken);
}

public sealed class MemberProfileVerificationService : IMemberProfileVerificationService
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDomainEventPublisher _events;
    private readonly IMapper _mapper;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IUnitOfWork _kurinData;
    private readonly IMembershipRepository _memberships;
    private readonly IMemberFieldAccess _fieldAccess;
    private readonly IKurinAudit _audit;

    public MemberProfileVerificationService(
        IMemberUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IMapper mapper,
        IResourceScopeReader scopeReader,
        IUnitOfWork kurinData,
        IMemberFieldAccess fieldAccess,
        IKurinAudit audit)
    {
        _fieldAccess = fieldAccess;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _events = events;
        _mapper = mapper;
        _scopeReader = scopeReader;
        _kurinData = kurinData;
        _memberships = kurinData.Memberships;
    }

    public async Task<ServiceResult<MemberResponse>> VerifyAsync(
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
        member.ProfileVerificationNote = NormalizeNote(note);
        member.UpdatedDate = DateTime.UtcNow;

        // Checking a profile again is an act of its own, so it is recorded even when it was already current.
        await RecordAsync(AuditActions.Member.ProfileVerified, member, statusBefore, member.ProfileVerificationNote, cancellationToken);

        var result = await SaveAsync(member, groupKey, cancellationToken);
        if (result.Type == ResultType.Success)
        {
            await PublishProfileVerifiedAsync(member, cancellationToken);
        }

        return result;
    }

    public async Task<ServiceResult<MemberResponse>> ResetAsync(
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
            await RecordAsync(AuditActions.Member.VerificationReset, member, statusBefore, null, cancellationToken);
        }

        return await SaveAsync(member, groupKey, cancellationToken);
    }

    /// <summary>Refuses what may not be done, or answers with the гурток the person is placed in.</summary>
    private async Task<(ServiceResult<MemberResponse>? Failure, Guid? GroupKey)> ValidateAsync(MemberEntity? member, CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return (new ServiceResult<MemberResponse>(ResultType.Unauthorized), null);
        }

        if (member is null)
        {
            return (new ServiceResult<MemberResponse>(ResultType.NotFound), null);
        }

        // Whether profiles are checked at all is the asking kurin's own setting, and it is that
        // kurin's membership that puts this person in reach — a person the caller cannot reach
        // is refused below in any case.
        var membership = await _memberships.GetActiveForMemberAsync(member.MemberKey, cancellationToken);
        var here = membership.FirstOrDefault(m => m.KurinKey == _currentUserContext.KurinKey)
            ?? membership.FirstOrDefault();

        if (here is null)
        {
            return (ServiceResult<MemberResponse>.Failure(
                ResultType.BadRequest,
                "MemberBelongsNowhere",
                "This person does not currently belong to a kurin."), null);
        }

        var kurin = await _kurinData.Kurins.GetByKeyAsync(here.KurinKey, cancellationToken);
        if (kurin is null || !kurin.ProfileVerificationEnabled)
        {
            return (ServiceResult<MemberResponse>.Failure(
                ResultType.BadRequest,
                "ProfileVerificationDisabled",
                "Profile verification is disabled for this kurin."), null);
        }

        if (member.UserKey.HasValue && member.UserKey.Value == _currentUserContext.UserId.Value)
        {
            return (new ServiceResult<MemberResponse>(ResultType.Forbidden), null);
        }

        if (await CanVerifyAsync(here, cancellationToken))
        {
            return (null, here.GroupKey);
        }

        return (new ServiceResult<MemberResponse>(ResultType.Forbidden), null);
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

    private async Task<ServiceResult<MemberResponse>> SaveAsync(MemberEntity member, Guid? groupKey, CancellationToken cancellationToken)
    {
        _unitOfWork.Members.Update(member, cancellationToken);
        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return new ServiceResult<MemberResponse>(ResultType.InternalServerError);
        }

        var mask = await _fieldAccess.ForViewerAsync(cancellationToken);
        return new ServiceResult<MemberResponse>(ResultType.Success, _mapper.Map<MemberResponse>(member).MaskFor(mask, groupKey));
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
                MemberAuditFields.LabelOf(member),
                member.MemberKey,
                AuditChanges.Of((MemberAuditFields.ProfileVerification, statusBefore, member.ProfileVerificationStatus)),
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

    private static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
