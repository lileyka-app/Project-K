using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using RequestEntity = ProjectK.Common.Entities.KurinModule.MembershipRequest;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest.Create;

/// <summary>
/// The asking kurin's Звʼязковий asks for a person — by the code the person handed over, or by key
/// for someone who once stood in the asking kurin. A person who stands in no kurin has nobody to
/// ask and is taken in at once; anyone else waits for the Звʼязковий of their kurin.
/// </summary>
/// <param name="FromKurinKey">
/// The kurin to ask, needed only when the person stands in more than one.
/// </param>
public sealed record CreateMembershipRequestCommand(
    Guid ToKurinKey,
    string? PublicId,
    Guid? MemberKey,
    MembershipRequestKind Kind,
    MembershipKind MembershipKind,
    Guid? FromKurinKey = null) : IRequest<ServiceResult<MembershipRequestSent>>;

public sealed class CreateMembershipRequestCommandValidator : AbstractValidator<CreateMembershipRequestCommand>
{
    public CreateMembershipRequestCommandValidator()
    {
        RuleFor(c => c.ToKurinKey).NotEmpty();
        RuleFor(c => c.Kind).IsInEnum();
        RuleFor(c => c.MembershipKind).IsInEnum();
        RuleFor(c => c).Must(c => !string.IsNullOrWhiteSpace(c.PublicId) || c.MemberKey is { } key && key != Guid.Empty)
            .WithMessage("Either a public code or a member is required.");
    }
}

public sealed class CreateMembershipRequestCommandHandler
    : IRequestHandler<CreateMembershipRequestCommand, ServiceResult<MembershipRequestSent>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly MembershipMoves _moves;
    private readonly IDomainEventPublisher _events;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAccessChangeSignal _accessChanges;
    private readonly TimeProvider _time;

    public CreateMembershipRequestCommandHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        MembershipMoves moves,
        IDomainEventPublisher events,
        ICurrentUserContext currentUser,
        IAccessChangeSignal accessChanges,
        TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _moves = moves;
        _events = events;
        _currentUser = currentUser;
        _accessChanges = accessChanges;
        _time = time;
    }

    public async Task<ServiceResult<MembershipRequestSent>> Handle(CreateMembershipRequestCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.ToKurinKey)
        {
            return new ServiceResult<MembershipRequestSent>(ResultType.Forbidden);
        }

        var memberKey = await ResolveAsync(request, cancellationToken);
        var person = memberKey is { } key ? await _members.FindAsync(key, cancellationToken) : null;
        if (person is null)
        {
            return ServiceResult<MembershipRequestSent>.Failure(
                ResultType.NotFound, "NoSuchCode", "No one has that code.");
        }

        var standing = await _unitOfWork.Memberships.GetActiveForMemberAsync(person.MemberKey, cancellationToken);
        if (standing.Any(m => m.KurinKey == request.ToKurinKey))
        {
            return ServiceResult<MembershipRequestSent>.Failure(
                ResultType.Conflict, "AlreadyAMember", "They already belong to this kurin.");
        }

        if (standing.Count == 0)
        {
            var joined = await _moves.OpenAsync(person, request.ToKurinKey, request.MembershipKind, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (joined.UserKey is { } userKey)
            {
                _accessChanges.MarkUser(userKey);
            }

            return new ServiceResult<MembershipRequestSent>(ResultType.Created, new MembershipRequestSent(null, Joined: true));
        }

        Guid fromKurinKey;
        if (request.FromKurinKey is { } chosen)
        {
            if (standing.All(m => m.KurinKey != chosen))
            {
                return ServiceResult<MembershipRequestSent>.Failure(
                    ResultType.BadRequest, "NotInThatKurin", "They do not stand in that kurin.");
            }

            fromKurinKey = chosen;
        }
        else if (standing.Count == 1)
        {
            fromKurinKey = standing.First().KurinKey;
        }
        else
        {
            return ServiceResult<MembershipRequestSent>.Failure(
                ResultType.Conflict, "ChooseKurin", "They stand in several kurins; say which one to ask.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var pending = await _unitOfWork.MembershipRequests.GetPendingAsync(person.MemberKey, request.ToKurinKey, cancellationToken);
        if (pending.Any(r => r.IsOpen(now)))
        {
            return ServiceResult<MembershipRequestSent>.Failure(
                ResultType.Conflict, "RequestPending", "This kurin already waits for an answer about them.");
        }

        var created = new RequestEntity
        {
            Kind = request.Kind,
            MemberKey = person.MemberKey,
            FromKurinKey = fromKurinKey,
            ToKurinKey = request.ToKurinKey,
            MembershipKind = request.MembershipKind,
            CreatedAtUtc = now,
            CreatedByUserKey = _currentUser.UserId,
            ExpiresAtUtc = now + RequestEntity.Lifetime
        };
        _unitOfWork.MembershipRequests.Create(created);

        await _events.PublishAsync(
            new MembershipRequested(
                created.MembershipRequestKey, created.Kind, person.MemberKey, person.FullName,
                fromKurinKey, request.ToKurinKey, _currentUser.UserId),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<MembershipRequestSent>(ResultType.Created, new MembershipRequestSent(created.MembershipRequestKey, Joined: false));
    }

    /// <summary>
    /// The code, or a key that once stood in the asking kurin. Any other key is refused as unknown:
    /// keys are seen on other kurins' screens, and asking by one would skip the code the person holds.
    /// </summary>
    private async Task<Guid?> ResolveAsync(CreateMembershipRequestCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.PublicId))
        {
            return (await _members.FindByPublicIdAsync(request.PublicId, cancellationToken))?.MemberKey;
        }

        var memberKey = request.MemberKey!.Value;
        var stoodHere = (await _unitOfWork.Memberships.GetRecordsForMemberAsync(memberKey, cancellationToken))
            .Any(m => m.KurinKey == request.ToKurinKey);
        return stoodHere ? memberKey : null;
    }
}
