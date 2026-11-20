using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using MemberEntity = ProjectK.Common.Entities.MemberModule.Member;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

/// <summary>
/// Confirms or rejects a senior ступінь a person entered for themselves. Only the Звʼязковий of a kurin
/// the person stands in now may do it: the kurin-wide <c>Member:Update</c> in the kurin the caller acts
/// in, which the scope check holds to a kurin the person stands in — the same right that changes a
/// senior's youth record. Nobody confirms their own. Each answer is written to the kurin's journal with
/// the card's ступені as they were and as they are.
/// </summary>
public sealed class PlastLevelConfirmationService
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IResourceAccessService _access;
    private readonly ICurrentPermissions _permissions;
    private readonly ICurrentUserContext _currentUser;
    private readonly IKurinAudit _audit;
    private readonly TimeProvider _time;

    public PlastLevelConfirmationService(
        IMemberUnitOfWork unitOfWork,
        IResourceAccessService access,
        ICurrentPermissions permissions,
        ICurrentUserContext currentUser,
        IKurinAudit audit,
        TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _permissions = permissions;
        _currentUser = currentUser;
        _audit = audit;
        _time = time;
    }

    /// <summary>Makes the waiting ступінь count: for the улад, the highest ступінь and the latest one.</summary>
    public async Task<ServiceResult<PlastLevelHistoryDto>> ConfirmAsync(Guid memberKey, Guid levelKey, CancellationToken cancellationToken)
    {
        var (member, level, refusal) = await FindWaitingAsync<PlastLevelHistoryDto>(memberKey, levelKey, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        var before = MemberTrail.Of(member!);
        level!.AwaitsConfirmation = false;
        level.ConfirmedByUserKey = _currentUser.UserId;
        level.ConfirmedAtUtc = _time.GetUtcNow().UtcDateTime;
        member!.LatestPlastLevel = PlastLevelHistoryRules.LatestOf(member.PlastLevelHistory);
        member.UpdatedDate = level.ConfirmedAtUtc.Value;

        await SaveAsync(AuditActionNames.Member.LevelConfirmed, member, before, cancellationToken);
        return new ServiceResult<PlastLevelHistoryDto>(ResultType.Success, level.ToDto());
    }

    /// <summary>Removes the waiting ступінь from the card: a rejected one is not kept.</summary>
    public async Task<ServiceResult<Unit>> RejectAsync(Guid memberKey, Guid levelKey, CancellationToken cancellationToken)
    {
        var (member, level, refusal) = await FindWaitingAsync<Unit>(memberKey, levelKey, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        var before = MemberTrail.Of(member!);
        member!.PlastLevelHistory.Remove(level!);
        member.LatestPlastLevel = PlastLevelHistoryRules.LatestOf(member.PlastLevelHistory);
        member.UpdatedDate = _time.GetUtcNow().UtcDateTime;

        await SaveAsync(AuditActionNames.Member.LevelRejected, member, before, cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

    private async Task<(MemberEntity? Member, PlastLevelHistory? Level, ServiceResult<T>? Refusal)> FindWaitingAsync<T>(
        Guid memberKey,
        Guid levelKey,
        CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(memberKey, cancellationToken);
        if (member is null)
        {
            return (null, null, ServiceResult<T>.Failure(ResultType.NotFound, MemberErrors.MemberNotFound, "No such member."));
        }

        var level = member.PlastLevelHistory.FirstOrDefault(entry => entry.PlastLevelHistoryKey == levelKey);
        if (level is null)
        {
            return (member, null, ServiceResult<T>.Failure(ResultType.NotFound, MemberErrors.LevelNotFound, "The level is not on this member's card."));
        }

        if (!level.AwaitsConfirmation)
        {
            return (member, level, ServiceResult<T>.Failure(ResultType.Conflict, MemberErrors.LevelNotAwaitingConfirmation, "The level already counts."));
        }

        if (!await MayAnswerAsync(member, cancellationToken))
        {
            return (member, level, ServiceResult<T>.Failure(
                ResultType.Forbidden,
                MemberErrors.LevelConfirmationNotAllowed,
                "A waiting level is answered by the Zvyazkovyi of a kurin the member stands in now, never by the member."));
        }

        return (member, level, null);
    }

    private async Task<bool> MayAnswerAsync(MemberEntity member, CancellationToken cancellationToken)
    {
        if (member.UserKey is { } owner && owner == _currentUser.UserId)
        {
            return false;
        }

        return await _permissions.WidestScopeAsync(ResourceType.Member, ResourceAction.Update, cancellationToken) == AccessScope.KurinWide
               && (await _access.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, member.MemberKey, cancellationToken)).IsAllowed;
    }

    private async Task SaveAsync(string action, MemberEntity member, (string Field, object? Value)[] before, CancellationToken cancellationToken)
    {
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Member,
                action,
                member.MemberKey,
                MemberTrail.LabelOf(member),
                member.MemberKey,
                MemberTrail.Between(before, MemberTrail.Of(member))),
            cancellationToken);

        _unitOfWork.Members.Update(member, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
