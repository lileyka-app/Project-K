using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment;

public sealed record GetKurinMentorCandidatesQuery(Guid KurinKey) : IRequest<ServiceResult<IEnumerable<MemberLookupDto>>>;

public sealed class GetKurinMentorCandidatesQueryHandler : IRequestHandler<GetKurinMentorCandidatesQuery, ServiceResult<IEnumerable<MemberLookupDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly IBlobReadLinks _links;

    public GetKurinMentorCandidatesQueryHandler(IUnitOfWork uow, IBlobReadLinks links)
    {
        _uow = uow;
        _links = links;
    }

    public async Task<ServiceResult<IEnumerable<MemberLookupDto>>> Handle(GetKurinMentorCandidatesQuery request, CancellationToken cancellationToken)
    {
        var response = (await _uow.Memberships.GetMentorCandidatesAsync(request.KurinKey, cancellationToken))
            .Select(candidate => candidate.ToLookupDto())
            .ToList();
        return new ServiceResult<IEnumerable<MemberLookupDto>>(ResultType.Success, response.WithPhotoLinks(_links));
    }
}
