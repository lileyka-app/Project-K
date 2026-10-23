using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using MembershipEntity = ProjectK.Common.Entities.KurinModule.Membership;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Join;

/// <summary>
/// Takes an existing person into a kurin. Nothing about them changes — they keep every kurin they
/// already belong to, and everything they have earned anywhere.
/// </summary>
/// <param name="MemberKey">
/// Who to take in. May be empty when <paramref name="PublicId"/> names them instead — a провід
/// taking in someone from another kurin has their code, not their key.
/// </param>
public sealed record JoinKurinCommand(
    Guid MemberKey,
    Guid KurinKey,
    Guid? GroupKey,
    MembershipKind Kind = MembershipKind.Youth,
    string? PublicId = null) : IRequest<ServiceResult<Guid>>;

public sealed class JoinKurinCommandHandler : IRequestHandler<JoinKurinCommand, ServiceResult<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessChangeSignal _accessChanges;
    private readonly IMemberDirectory _members;
    private readonly IKurinAudit _audit;

    public JoinKurinCommandHandler(IUnitOfWork unitOfWork, IMemberDirectory members, IAccessChangeSignal accessChanges, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _accessChanges = accessChanges;
        _members = members;
        _audit = audit;
    }

    public async Task<ServiceResult<Guid>> Handle(JoinKurinCommand request, CancellationToken cancellationToken)
    {
        if (request.KurinKey == Guid.Empty
            || (request.MemberKey == Guid.Empty && string.IsNullOrWhiteSpace(request.PublicId)))
        {
            return ServiceResult<Guid>.Failure(
                ResultType.BadRequest,
                "MembershipKeysRequired",
                "A kurin and either a member or a public code are required.");
        }

        var memberKey = request.MemberKey;
        if (memberKey == Guid.Empty)
        {
            var card = await _members.FindByPublicIdAsync(request.PublicId!, cancellationToken);
            if (card is null)
            {
                return ServiceResult<Guid>.Failure(
                    ResultType.NotFound, "NoSuchCode", "No one has that code.");
            }

            memberKey = card.MemberKey;
        }

        var person = await _members.FindAsync(memberKey, cancellationToken);
        if (person is null)
        {
            return ServiceResult<Guid>.Failure(
                ResultType.NotFound, "MemberNotFound", "No such member.");
        }

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<Guid>.Failure(
                ResultType.NotFound, "KurinNotFound", "No such kurin.");
        }

        // A гурток of another kurin would place them somewhere their kurin does not reach.
        string? groupName = null;
        if (request.GroupKey.HasValue)
        {
            var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey.Value, cancellationToken);
            groupName = group?.Name;
            if (group is null || group.KurinKey != request.KurinKey)
            {
                return ServiceResult<Guid>.Failure(
                    ResultType.BadRequest, "GroupNotInKurin", "That гурток does not belong to this kurin.");
            }
        }

        var already = await _unitOfWork.Memberships.GetActiveAsync(
            memberKey, request.KurinKey, cancellationToken);
        if (already is not null)
        {
            return ServiceResult<Guid>.Failure(
                ResultType.Conflict, "AlreadyAMember", "They already belong to this kurin.");
        }

        var membership = new MembershipEntity
        {
            MemberKey = memberKey,
            UserKey = person.UserKey,
            KurinKey = request.KurinKey,
            GroupKey = request.GroupKey,
            Kind = request.Kind,
            JoinedAtUtc = DateTime.UtcNow
        };

        _unitOfWork.Memberships.Open(membership);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Membership,
                AuditActions.Membership.Joined,
                membership.MembershipKey,
                person.FullName,
                memberKey,
                AuditChanges.Added(("kind", request.Kind), ("group", groupName)),
                KurinKey: request.KurinKey),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (membership.UserKey is { } userKey)
        {
            _accessChanges.MarkUser(userKey);
        }

        return new ServiceResult<Guid>(ResultType.Created, membership.MembershipKey);
    }
}
