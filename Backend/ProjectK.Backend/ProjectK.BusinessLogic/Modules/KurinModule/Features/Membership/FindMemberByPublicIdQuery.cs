using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;

/// <summary>
/// Resolves a public code to the card a провід confirms before asking for someone. Exact match
/// only — a code is something a person hands over, not something to be guessed at.
/// </summary>
public sealed record FindMemberByPublicIdQuery(Guid KurinKey, string PublicId)
    : IRequest<ServiceResult<MembershipCandidateDto>>;

public sealed class FindMemberByPublicIdQueryHandler
    : IRequestHandler<FindMemberByPublicIdQuery, ServiceResult<MembershipCandidateDto>>
{
    private readonly IMemberDirectory _members;
    private readonly IUnitOfWork _kurinData;
    private readonly MembershipAskRouting _routing;

    public FindMemberByPublicIdQueryHandler(IMemberDirectory members, IUnitOfWork kurinData, MembershipAskRouting routing)
    {
        _members = members;
        _kurinData = kurinData;
        _routing = routing;
    }

    public async Task<ServiceResult<MembershipCandidateDto>> Handle(
        FindMemberByPublicIdQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PublicId))
        {
            return ServiceResult<MembershipCandidateDto>.Failure(
                ResultType.BadRequest, MembershipErrors.PublicIdRequired, "A public code is required.");
        }

        var card = await _members.FindByPublicIdAsync(request.PublicId, cancellationToken);
        if (card is null)
        {
            // The same answer whether the code is malformed or simply nobody's — a lookup that
            // distinguished them would let someone map out which codes exist.
            return ServiceResult<MembershipCandidateDto>.Failure(
                ResultType.NotFound, MembershipErrors.NoSuchCode, "No one has that code.");
        }

        var standing = await _kurinData.Memberships.GetActiveForMemberAsync(card.MemberKey, cancellationToken);
        var elsewhere = new List<MembershipRequestKurinDto>();
        foreach (var membership in standing.Where(m => m.KurinKey != request.KurinKey))
        {
            var kurin = await _kurinData.Kurins.GetByKeyAsync(membership.KurinKey, cancellationToken);
            elsewhere.Add(kurin.ToRequestKurinDto(membership.KurinKey));
        }

        return new ServiceResult<MembershipCandidateDto>(
            ResultType.Success,
            new MembershipCandidateDto(
                card,
                standing.Any(m => m.KurinKey == request.KurinKey),
                elsewhere,
                await _routing.OfferAsync(card.MemberKey, request.KurinKey, cancellationToken)));
    }
}
