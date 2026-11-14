using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;

public record GetKurinKvMembersQuery(Guid kurinKey) : IRequest<ServiceResult<IEnumerable<MemberLookupDto>>>;

public class GetKurinKvMembersQueryHandler : IRequestHandler<GetKurinKvMembersQuery, ServiceResult<IEnumerable<MemberLookupDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly IBlobReadLinks _links;
    public GetKurinKvMembersQueryHandler(IUnitOfWork uow, IBlobReadLinks links)
    {
        _uow = uow;
        _links = links;
    }

    public async Task<ServiceResult<IEnumerable<MemberLookupDto>>> Handle(GetKurinKvMembersQuery request, CancellationToken cancellationToken)
    {
        // One row per active КВ office (Зв'язковий / Впорядник / Інструктор), UserRole = office role name.
        var kvMembers = await _uow.Leaderships.GetOfficeMembersLookupAsync(request.kurinKey, LeadershipType.KV, cancellationToken);

        return new ServiceResult<IEnumerable<MemberLookupDto>>(ResultType.Success, kvMembers.WithPhotoLinks(_links));
    }
}
