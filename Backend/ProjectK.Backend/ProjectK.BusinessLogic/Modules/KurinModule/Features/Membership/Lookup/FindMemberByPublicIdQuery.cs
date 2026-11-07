using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Lookup;

/// <summary>
/// Resolves a public code to the card a провід confirms before asking for someone. Exact match
/// only — a code is something a person hands over, not something to be guessed at.
/// </summary>
public sealed record FindMemberByPublicIdQuery(Guid KurinKey, string PublicId)
    : IRequest<ServiceResult<MembershipCandidate>>;

/// <summary>
/// The card, whether this kurin already has them, and the kurins they stand in now. Those kurins are
/// named only by number: the person handed over their code so this kurin could ask for them, and
/// asking means asking a particular kurin — «перевести» from it, or «прийняти ще й до нас» alongside it.
/// No contact details, and nothing about kurins they once left.
/// </summary>
public sealed record MembershipCandidate(MemberCard Member, bool AlreadyInThisKurin, IReadOnlyList<MembershipRequestKurin> CurrentKurins);

public sealed class FindMemberByPublicIdQueryHandler
    : IRequestHandler<FindMemberByPublicIdQuery, ServiceResult<MembershipCandidate>>
{
    private readonly IMemberDirectory _members;
    private readonly IUnitOfWork _kurinData;

    public FindMemberByPublicIdQueryHandler(IMemberDirectory members, IUnitOfWork kurinData)
    {
        _members = members;
        _kurinData = kurinData;
    }

    public async Task<ServiceResult<MembershipCandidate>> Handle(
        FindMemberByPublicIdQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PublicId))
        {
            return ServiceResult<MembershipCandidate>.Failure(
                ResultType.BadRequest, "PublicIdRequired", "A public code is required.");
        }

        var card = await _members.FindByPublicIdAsync(request.PublicId, cancellationToken);
        if (card is null)
        {
            // The same answer whether the code is malformed or simply nobody's — a lookup that
            // distinguished them would let someone map out which codes exist.
            return ServiceResult<MembershipCandidate>.Failure(
                ResultType.NotFound, "NoSuchCode", "No one has that code.");
        }

        var standing = await _kurinData.Memberships.GetActiveForMemberAsync(card.MemberKey, cancellationToken);
        var elsewhere = new List<MembershipRequestKurin>();
        foreach (var membership in standing.Where(m => m.KurinKey != request.KurinKey))
        {
            var kurin = await _kurinData.Kurins.GetByKeyAsync(membership.KurinKey, cancellationToken);
            elsewhere.Add(MembershipRequestKurin.Of(membership.KurinKey, kurin));
        }

        return new ServiceResult<MembershipCandidate>(
            ResultType.Success,
            new MembershipCandidate(card, standing.Any(m => m.KurinKey == request.KurinKey), elsewhere));
    }
}
