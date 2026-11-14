using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Records.Shared;
using RequestEntity = ProjectK.Common.Entities.KurinModule.MembershipRequest;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;

/// <summary>
/// The asking kurin's Звʼязковий asks for a person — by the code the person handed over, or by key
/// for someone who once stood in the asking kurin. Who answers is <see cref="MembershipAskRouting"/>'s:
/// the Звʼязковий of a kurin of the same line, the person themself across the youth and senior lines,
/// or nobody when they are taken in at once.
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
    Guid? FromKurinKey = null) : IRequest<ServiceResult<MembershipRequestSentDto>>;

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
    : IRequestHandler<CreateMembershipRequestCommand, ServiceResult<MembershipRequestSentDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly MembershipMoves _moves;
    private readonly MembershipAskRouting _routing;
    private readonly IDomainEventPublisher _events;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAccessChangeSignal _accessChanges;
    private readonly TimeProvider _time;

    public CreateMembershipRequestCommandHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        MembershipMoves moves,
        MembershipAskRouting routing,
        IDomainEventPublisher events,
        ICurrentUserContext currentUser,
        IAccessChangeSignal accessChanges,
        TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _moves = moves;
        _routing = routing;
        _events = events;
        _currentUser = currentUser;
        _accessChanges = accessChanges;
        _time = time;
    }

    public async Task<ServiceResult<MembershipRequestSentDto>> Handle(CreateMembershipRequestCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.ToKurinKey)
        {
            return ServiceResult<MembershipRequestSentDto>.Failure(ResultType.Forbidden, MembershipErrors.OtherKurin, "A kurin reads and answers only its own requests.");
        }

        var memberKey = await ResolveAsync(request, cancellationToken);
        var person = memberKey is { } key ? await _members.FindAsync(key, cancellationToken) : null;
        if (person is null)
        {
            return ServiceResult<MembershipRequestSentDto>.Failure(
                ResultType.NotFound, MembershipErrors.NoSuchCode, "No one has that code.");
        }

        var standing = await _unitOfWork.Memberships.GetActiveForMemberAsync(person.MemberKey, cancellationToken);
        if (standing.Any(m => m.KurinKey == request.ToKurinKey))
        {
            return ServiceResult<MembershipRequestSentDto>.Failure(
                ResultType.Conflict, MembershipErrors.AlreadyAMember, "They already belong to this kurin.");
        }

        var route = await _routing.ResolveAsync(person.MemberKey, request.ToKurinKey, cancellationToken);
        if (route.ToSenior && request.MembershipKind != MembershipKind.Youth)
        {
            return ServiceResult<MembershipRequestSentDto>.Failure(
                ResultType.BadRequest, MembershipErrors.SeniorKurinTakesMembers, "A senior kurin takes people in only as members.");
        }

        if (route.JoinsAtOnce)
        {
            var joined = await _moves.OpenAsync(person, request.ToKurinKey, request.MembershipKind, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (joined.UserKey is { } userKey)
            {
                _accessChanges.MarkUser(userKey);
            }

            return new ServiceResult<MembershipRequestSentDto>(ResultType.Created, new MembershipRequestSentDto(null, Joined: true));
        }

        if (request.Kind == MembershipRequestKind.Transfer && !route.TransferAllowed)
        {
            return ServiceResult<MembershipRequestSentDto>.Failure(
                ResultType.BadRequest,
                MembershipErrors.TransferAcrossLines,
                "Youth and senior kurins do not take people from each other; ask to take them in alongside.");
        }

        Guid? fromKurinKey = null;
        if (!route.PersonAnswers)
        {
            if (request.FromKurinKey is { } chosen)
            {
                if (!route.AskableKurinKeys.Contains(chosen))
                {
                    return ServiceResult<MembershipRequestSentDto>.Failure(
                        ResultType.BadRequest, MembershipErrors.NotInThatKurin, "That kurin does not answer for them.");
                }

                fromKurinKey = chosen;
            }
            else if (route.AskableKurinKeys.Count == 1)
            {
                fromKurinKey = route.AskableKurinKeys[0];
            }
            else
            {
                return ServiceResult<MembershipRequestSentDto>.Failure(
                    ResultType.Conflict, MembershipErrors.ChooseKurin, "They stand in several kurins; say which one to ask.");
            }
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var pending = await _unitOfWork.MembershipRequests.GetPendingAsync(person.MemberKey, request.ToKurinKey, cancellationToken);
        if (pending.Any(r => r.IsOpen(now)))
        {
            return ServiceResult<MembershipRequestSentDto>.Failure(
                ResultType.Conflict, MembershipErrors.RequestPending, "This kurin already waits for an answer about them.");
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
                created.MembershipRequestKey, created.Kind, person.MemberKey, person.UserKey, person.FullName,
                fromKurinKey, request.ToKurinKey, _currentUser.UserId),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<MembershipRequestSentDto>(ResultType.Created, new MembershipRequestSentDto(created.MembershipRequestKey, Joined: false, route.PersonAnswers));
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
