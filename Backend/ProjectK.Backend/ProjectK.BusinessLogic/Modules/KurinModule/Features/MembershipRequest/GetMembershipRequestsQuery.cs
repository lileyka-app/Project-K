using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using RequestEntity = ProjectK.Common.Entities.KurinModule.MembershipRequest;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;

/// <summary>
/// A kurin's requests both ways: those its Звʼязковий has to answer, and those it sent. The person is
/// named on both sides — the answering kurin knows them, and the asking one was handed their code.
/// </summary>
public sealed record GetMembershipRequestsQuery(Guid KurinKey) : IRequest<ServiceResult<MembershipRequestsDto>>;

public sealed class GetMembershipRequestsQueryHandler
    : IRequestHandler<GetMembershipRequestsQuery, ServiceResult<MembershipRequestsDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly MembershipMoves _moves;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public GetMembershipRequestsQueryHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        MembershipMoves moves,
        ICurrentUserContext currentUser,
        TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _moves = moves;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<MembershipRequestsDto>> Handle(GetMembershipRequestsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return ServiceResult<MembershipRequestsDto>.Failure(ResultType.Forbidden, KurinErrors.MembershipRequestOtherKurin, "A kurin reads and answers only its own requests.");
        }

        var requests = await _unitOfWork.MembershipRequests.GetForKurinAsync(request.KurinKey, cancellationToken);
        var names = (await _members.GetByKeysAsync(requests.Select(r => r.MemberKey).Distinct().ToList(), cancellationToken))
            .ToDictionary(m => m.MemberKey, m => m.FullName);

        var kurins = new Dictionary<Guid, MembershipRequestKurinDto>();
        foreach (var kurinKey in requests.SelectMany(r => r.FromKurinKey is { } from ? new[] { from, r.ToKurinKey } : new[] { r.ToKurinKey }).Distinct())
        {
            var kurin = await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken);
            kurins[kurinKey] = kurin.ToRequestKurinDto(kurinKey);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var incoming = new List<MembershipRequestDto>();
        var outgoing = new List<MembershipRequestDto>();
        foreach (var r in requests)
        {
            var answering = r.FromKurinKey == request.KurinKey;
            var owed = answering && r.Kind == MembershipRequestKind.Transfer && r.IsOpen(now)
                ? await _moves.OwedToLeaveAsync(r.MemberKey, request.KurinKey, cancellationToken)
                : 0;
            var dto = new MembershipRequestDto(
                r.MembershipRequestKey,
                r.Kind,
                StatusOf(r, now),
                r.MemberKey,
                names.GetValueOrDefault(r.MemberKey, string.Empty),
                r.FromKurinKey is { } fromKey ? kurins[fromKey] : null,
                kurins[r.ToKurinKey],
                r.MembershipKind,
                r.CreatedAtUtc,
                r.ExpiresAtUtc,
                r.DecidedAtUtc,
                owed);
            (answering ? incoming : outgoing).Add(dto);
        }

        return new ServiceResult<MembershipRequestsDto>(ResultType.Success, new MembershipRequestsDto(incoming, outgoing));
    }

    private static string StatusOf(RequestEntity request, DateTime now) =>
        request.Status == MembershipRequestStatus.Pending && now >= request.ExpiresAtUtc
            ? "Expired"
            : request.Status.ToString();
}
