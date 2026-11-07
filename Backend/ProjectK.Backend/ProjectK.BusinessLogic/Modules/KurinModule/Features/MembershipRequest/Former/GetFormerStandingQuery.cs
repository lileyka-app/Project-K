using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest.Former;

/// <summary>
/// The kurin's former members as its Звʼязковий sees them before taking someone back: each with the
/// kurins they stand in now. Taking back someone who stands nowhere is immediate; anyone else is asked
/// for, «перевести» or «ще й до нас», so the Звʼязковий has to know which kurin answers.
/// </summary>
public sealed record GetFormerStandingQuery(Guid KurinKey) : IRequest<ServiceResult<IReadOnlyList<FormerMemberStanding>>>;

public sealed class GetFormerStandingQueryHandler
    : IRequestHandler<GetFormerStandingQuery, ServiceResult<IReadOnlyList<FormerMemberStanding>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;

    public GetFormerStandingQueryHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<FormerMemberStanding>>> Handle(GetFormerStandingQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return new ServiceResult<IReadOnlyList<FormerMemberStanding>>(ResultType.Forbidden);
        }

        var former = await _unitOfWork.Memberships.GetFormerInKurinAsync(request.KurinKey, cancellationToken);
        var kurins = new Dictionary<Guid, MembershipRequestKurin>();
        var result = new List<FormerMemberStanding>(former.Count);
        foreach (var person in former)
        {
            var standsIn = new List<MembershipRequestKurin>();
            foreach (var membership in await _unitOfWork.Memberships.GetActiveForMemberAsync(person.MemberKey, cancellationToken))
            {
                if (!kurins.TryGetValue(membership.KurinKey, out var kurin))
                {
                    var found = await _unitOfWork.Kurins.GetByKeyAsync(membership.KurinKey, cancellationToken);
                    kurin = MembershipRequestKurin.Of(membership.KurinKey, found);
                    kurins[membership.KurinKey] = kurin;
                }

                standsIn.Add(kurin);
            }

            result.Add(new FormerMemberStanding(person, standsIn));
        }

        return new ServiceResult<IReadOnlyList<FormerMemberStanding>>(ResultType.Success, result);
    }
}
