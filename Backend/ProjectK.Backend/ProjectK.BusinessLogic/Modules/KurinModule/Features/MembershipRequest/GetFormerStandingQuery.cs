using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;

/// <summary>
/// The kurin's former members as its Звʼязковий sees them before taking someone back: each with the
/// kurins they stand in now. Taking back someone who stands nowhere is immediate; anyone else is asked
/// for, «перевести» or «ще й до нас», so the Звʼязковий has to know which kurin answers.
/// </summary>
public sealed record GetFormerStandingQuery(Guid KurinKey) : IRequest<ServiceResult<IReadOnlyList<FormerMemberStandingDto>>>;

public sealed class GetFormerStandingQueryHandler
    : IRequestHandler<GetFormerStandingQuery, ServiceResult<IReadOnlyList<FormerMemberStandingDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly MembershipAskRouting _routing;

    public GetFormerStandingQueryHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, MembershipAskRouting routing)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _routing = routing;
    }

    public async Task<ServiceResult<IReadOnlyList<FormerMemberStandingDto>>> Handle(GetFormerStandingQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return ServiceResult<IReadOnlyList<FormerMemberStandingDto>>.Failure(ResultType.Forbidden, MembershipErrors.OtherKurin, "A kurin reads and answers only its own requests.");
        }

        var former = await _unitOfWork.Memberships.GetFormerInKurinAsync(request.KurinKey, cancellationToken);
        var kurins = new Dictionary<Guid, MembershipRequestKurinDto>();
        var result = new List<FormerMemberStandingDto>(former.Count);
        foreach (var person in former)
        {
            var standsIn = new List<MembershipRequestKurinDto>();
            foreach (var membership in await _unitOfWork.Memberships.GetActiveForMemberAsync(person.MemberKey, cancellationToken))
            {
                if (!kurins.TryGetValue(membership.KurinKey, out var kurin))
                {
                    var found = await _unitOfWork.Kurins.GetByKeyAsync(membership.KurinKey, cancellationToken);
                    kurin = found.ToRequestKurinDto(membership.KurinKey);
                    kurins[membership.KurinKey] = kurin;
                }

                standsIn.Add(kurin);
            }

            result.Add(new FormerMemberStandingDto(person, standsIn, await _routing.OfferAsync(person.MemberKey, request.KurinKey, cancellationToken)));
        }

        return new ServiceResult<IReadOnlyList<FormerMemberStandingDto>>(ResultType.Success, result);
    }
}
