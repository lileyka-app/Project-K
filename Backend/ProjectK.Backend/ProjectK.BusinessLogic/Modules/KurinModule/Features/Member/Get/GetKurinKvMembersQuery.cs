using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Get;

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
