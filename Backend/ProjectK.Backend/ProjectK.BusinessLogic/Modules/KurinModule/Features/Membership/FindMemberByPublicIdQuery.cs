using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;
using MediatR;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;

/// <summary>
/// Resolves a public code to the card a провід confirms before asking for someone. Exact match
/// only — a code is something a person hands over, not something to be guessed at.
/// </summary>
public sealed record FindMemberByPublicIdQuery(Guid KurinKey, string PublicId)
    : IRequest<ServiceResult<MembershipCandidateDto>>;

/// <summary>
/// The card, whether this kurin already has them, and the kurins they stand in now. Those kurins are
/// named only by number: the person handed over their code so this kurin could ask for them, and
/// asking means asking a particular kurin — «перевести» from it, or «прийняти ще й до нас» alongside it.
/// No contact details, and nothing about kurins they once left.
/// </summary>
public sealed record MembershipCandidateDto(MemberCard Member, bool AlreadyInThisKurin, IReadOnlyList<MembershipRequestKurinDto> CurrentKurins, MembershipAskOfferDto Offer);

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
                ResultType.BadRequest, "PublicIdRequired", "A public code is required.");
        }

        var card = await _members.FindByPublicIdAsync(request.PublicId, cancellationToken);
        if (card is null)
        {
            // The same answer whether the code is malformed or simply nobody's — a lookup that
            // distinguished them would let someone map out which codes exist.
            return ServiceResult<MembershipCandidateDto>.Failure(
                ResultType.NotFound, "NoSuchCode", "No one has that code.");
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
