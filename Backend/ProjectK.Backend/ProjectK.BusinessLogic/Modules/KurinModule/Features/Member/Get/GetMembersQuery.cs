using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Get;

public class GetMembersQuery : IRequest<ServiceResult<IEnumerable<MemberResponse>>>
{
    public Guid GroupKey { get; set; }
    public Guid KurinKey { get; set; }
    public GetMembersQuery(Guid groupKey, Guid kurinKey)
    {
        GroupKey = groupKey;
        KurinKey = kurinKey;
    }
}

public class GetMembersQueryHandler : IRequestHandler<GetMembersQuery, ServiceResult<IEnumerable<MemberResponse>>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IMemberFieldAccess _fieldAccess;

    public GetMembersQueryHandler(IMemberUnitOfWork unitOfWork, IMapper mapper, IMemberFieldAccess fieldAccess)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _fieldAccess = fieldAccess;
    }

    public async Task<ServiceResult<IEnumerable<MemberResponse>>> Handle(GetMembersQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<MemberListItemDto> members;
        if (request.KurinKey == Guid.Empty)
        {
            members = await _unitOfWork.Members.GetListItemsByGroupKeyAsync(request.GroupKey, cancellationToken);
        }
        else if (request.GroupKey == Guid.Empty)
        {
            members = await _unitOfWork.Members.GetListItemsByKurinKeyAsync(request.KurinKey, cancellationToken);
        }
        else
        {
            return new ServiceResult<IEnumerable<MemberResponse>>(ResultType.BadRequest);
        }

        // Read whole and masked here, row by row: the registry export reads through this query too,
        // so whatever a viewer may not see never reaches a sheet either.
        var mask = await _fieldAccess.ForViewerAsync(cancellationToken);
        var rows = members.ToList();
        foreach (var row in rows)
        {
            mask.Apply(row, row.UserKey, row.GroupKey);
        }

        var response = _mapper.Map<IEnumerable<MemberResponse>>(rows);
        return new ServiceResult<IEnumerable<MemberResponse>>(ResultType.Success, response);
    }
}
