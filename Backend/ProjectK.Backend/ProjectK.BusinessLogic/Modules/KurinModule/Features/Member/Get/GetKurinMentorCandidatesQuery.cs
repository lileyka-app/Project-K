using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Get;

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
