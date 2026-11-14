using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;

public record GetKurinMentorCandidatesQuery(Guid KurinKey) : IRequest<ServiceResult<IEnumerable<MemberLookupDto>>>;

public class GetKurinMentorCandidatesQueryHandler : IRequestHandler<GetKurinMentorCandidatesQuery, ServiceResult<IEnumerable<MemberLookupDto>>>
{
    private readonly IMemberUnitOfWork _uow;
    private readonly IBlobReadLinks _links;

    public GetKurinMentorCandidatesQueryHandler(IMemberUnitOfWork uow, IBlobReadLinks links)
    {
        _uow = uow;
        _links = links;
    }

    public async Task<ServiceResult<IEnumerable<MemberLookupDto>>> Handle(GetKurinMentorCandidatesQuery request, CancellationToken cancellationToken)
    {
        var response = await _uow.Members.GetMentorCandidatesLookupAsync(request.KurinKey, cancellationToken);
        return new ServiceResult<IEnumerable<MemberLookupDto>>(ResultType.Success, response.WithPhotoLinks(_links));
    }
}
