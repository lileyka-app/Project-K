using MediatR;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;

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
        IReadOnlyList<MemberListRow> rows;
        if (request.KurinKey == Guid.Empty)
        {
            rows = await _unitOfWork.Members.GetListRowsByGroupKeyAsync(request.GroupKey, cancellationToken);
        }
        else if (request.GroupKey == Guid.Empty)
        {
            rows = await _unitOfWork.Members.GetListRowsByKurinKeyAsync(request.KurinKey, cancellationToken);
        }
        else
        {
            return ServiceResult<IEnumerable<MemberDto>>.Failure(ResultType.BadRequest, MemberErrors.RosterScopeRequired, "A roster is read for a group or for a kurin, not both.");
        }

        // Read whole and masked here, row by row: the registry export reads through this query too,
        // so whatever a viewer may not see never reaches a sheet either.
        var mask = await _fieldAccess.ForViewerAsync(cancellationToken);
        var response = new List<MemberDto>(rows.Count);
        foreach (var row in rows)
        {
            var member = row.ToDto(_links);
            mask.Apply(member, member.UserKey, row.GroupKey);
            response.Add(member);
        }

        await _tagVisibility.FillAsync(request.KurinKey, response, cancellationToken);
        return new ServiceResult<IEnumerable<MemberDto>>(ResultType.Success, response);
    }
}
