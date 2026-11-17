using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ArchiveModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.ArchiveModule.Features.Archive;

/// <summary>
/// Corrects a membership that has ended: when it began and ended, and of what kind it was. A
/// current membership is the card's to change.
/// </summary>
public sealed record CorrectArchivedMembershipCommand(
    Guid KurinKey,
    Guid MembershipKey,
    MembershipKind Kind,
    DateOnly JoinedOn,
    DateOnly LeftOn) : IRequest<ServiceResult<Unit>>;

public sealed class CorrectArchivedMembershipCommandValidator : AbstractValidator<CorrectArchivedMembershipCommand>
{
    public CorrectArchivedMembershipCommandValidator(TimeProvider time)
    {
        RuleFor(command => command.Kind).IsInEnum();
        RuleFor(command => command.LeftOn)
            .GreaterThanOrEqualTo(command => command.JoinedOn)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
    }
}

public sealed class CorrectArchivedMembershipCommandHandler : IRequestHandler<CorrectArchivedMembershipCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly ArchiveReach _reach;
    private readonly IKurinAudit _audit;
    private readonly IDuesDirectory _dues;

    public CorrectArchivedMembershipCommandHandler(IUnitOfWork unitOfWork, IMemberDirectory members, ArchiveReach reach, IKurinAudit audit, IDuesDirectory dues)
    {
        _dues = dues;
        _unitOfWork = unitOfWork;
        _members = members;
        _reach = reach;
        _audit = audit;
    }

    public async Task<ServiceResult<Unit>> Handle(CorrectArchivedMembershipCommand request, CancellationToken cancellationToken)
    {
        if (await _reach.ResolveAsync(request.KurinKey, ResourceAction.Update, cancellationToken) is not { } reach)
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, ArchiveErrors.OutOfReach, "The archive is not yours to correct.");
        }

        var membership = await _unitOfWork.Archive.FindMembershipAsync(request.KurinKey, request.MembershipKey, cancellationToken);
        if (membership is null)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, ArchiveErrors.NotFound, "No such membership in this kurin.");
        }

        if (membership.LeftAtUtc is not { } leftAt)
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, ArchiveErrors.StillCurrent, "A current membership is changed on the card.");
        }

        if (reach.IsOwn(membership.MemberKey))
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, ArchiveErrors.OwnRecord, "Nobody corrects their own record.");
        }

        var groups = await _unitOfWork.Archive.GetStintGroupKeysAsync(membership.MembershipKey, cancellationToken);
        if (!reach.Reaches(groups.Select(group => (Guid?)group).Append(membership.GroupKey)))
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, ArchiveErrors.OutOfReach, "The membership is outside your гуртки.");
        }

        var joinedOn = DateOnly.FromDateTime(membership.JoinedAtUtc);
        var leftOn = DateOnly.FromDateTime(leftAt);
        var changes = AuditChangeRules.Of(
            ("kind", membership.Kind, request.Kind),
            ("joinedOn", joinedOn, request.JoinedOn),
            ("leftOn", leftOn, request.LeftOn));
        if (changes.Count == 0)
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        // The dues ledger reads kind and dates on every open: changing them would charge the past
        // again, and the old charges would not go.
        if (await _dues.HasLedgerAsync(request.KurinKey, [membership.MembershipKey], cancellationToken))
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, ArchiveErrors.HasDues, "The membership carries dues; its kind and dates stay.");
        }

        var newJoined = request.JoinedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var newLeft = request.LeftOn.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var others = await _unitOfWork.Archive.GetOtherPeriodsAsync(membership.MemberKey, request.KurinKey, membership.MembershipKey, cancellationToken);
        if (others.Any(other => other.JoinedAtUtc <= newLeft && (other.LeftAtUtc ?? DateTime.MaxValue) >= newJoined))
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, ArchiveErrors.Overlap, "The dates overlap another membership of theirs in this kurin.");
        }

        // A date that did not change keeps its time of day; a corrected one is that day, and on the
        // day they joined it is no earlier than the moment they joined.
        var joinedAt = request.JoinedOn != joinedOn
            ? request.JoinedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            : membership.JoinedAtUtc;
        if (request.LeftOn != leftOn)
        {
            var corrected = request.LeftOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            leftAt = corrected < joinedAt ? joinedAt : corrected;
        }

        await _unitOfWork.Archive.CorrectMembershipAsync(membership.MembershipKey, request.Kind, joinedAt, leftAt, cancellationToken);

        var person = (await _members.GetByKeysAsync([membership.MemberKey], cancellationToken)).FirstOrDefault()?.FullName ?? string.Empty;
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Membership,
                AuditActionNames.Membership.Corrected,
                membership.MembershipKey,
                person,
                membership.MemberKey,
                changes,
                KurinKey: request.KurinKey),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
