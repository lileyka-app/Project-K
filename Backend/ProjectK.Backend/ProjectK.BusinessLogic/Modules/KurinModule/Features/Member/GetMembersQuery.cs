using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;

public class GetMembersQuery : IRequest<ServiceResult<IEnumerable<MemberDto>>>
{
    public Guid GroupKey { get; set; }
    public Guid KurinKey { get; set; }
    public GetMembersQuery(Guid groupKey, Guid kurinKey)
    {
        GroupKey = groupKey;
        KurinKey = kurinKey;
    }
}

public class GetMembersQueryHandler : IRequestHandler<GetMembersQuery, ServiceResult<IEnumerable<MemberDto>>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly IMemberFieldAccess _fieldAccess;
    private readonly IKurinTagVisibility _tagVisibility;

    public GetMembersQueryHandler(
        IMemberUnitOfWork unitOfWork,
        IBlobReadLinks links,
        IMemberFieldAccess fieldAccess,
        IKurinTagVisibility tagVisibility)
    {
        _unitOfWork = unitOfWork;
        _links = links;
        _fieldAccess = fieldAccess;
        _tagVisibility = tagVisibility;
    }

    public async Task<ServiceResult<IEnumerable<MemberDto>>> Handle(GetMembersQuery request, CancellationToken cancellationToken)
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
            return ServiceResult<IEnumerable<MemberDto>>.Failure(ResultType.BadRequest, MemberErrors.RosterScopeRequired, "A roster is read for a group or for a kurin, not both.");
        }

        // Read whole and masked here, row by row: the registry export reads through this query too,
        // so whatever a viewer may not see never reaches a sheet either.
        var mask = await _fieldAccess.ForViewerAsync(cancellationToken);
        var rows = members.ToList();
        foreach (var row in rows)
        {
            mask.Apply(row, row.UserKey, row.GroupKey);
        }

        var response = rows.Select(row => row.ToDto(_links)).ToList();
        await _tagVisibility.FillAsync(request.KurinKey, response, cancellationToken);
        return new ServiceResult<IEnumerable<MemberDto>>(ResultType.Success, response);
    }
}
