using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.MentorAssignment;

public record GetGroupMentorsQuery(Guid GroupKey) : IRequest<ServiceResult<IEnumerable<MemberLookupDto>>>;

public class GetGroupMentorsQueryHandler : IRequestHandler<GetGroupMentorsQuery, ServiceResult<IEnumerable<MemberLookupDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IBlobReadLinks _links;

    public GetGroupMentorsQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members, IBlobReadLinks links)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _links = links;
    }

    public async Task<ServiceResult<IEnumerable<MemberLookupDto>>> Handle(GetGroupMentorsQuery request, CancellationToken cancellationToken)
    {
        var assignments = await _unitOfWork.MentorAssignments.GetByGroupKeyAsync(request.GroupKey, cancellationToken);
        var activeAssignments = assignments.Where(a => a.RevokedAtUtc == null).ToList();

        var mentorMembers = new List<MemberSummary>();
        foreach (var assignment in activeAssignments)
        {
            var member = await _members.FindByAccountAsync(assignment.MentorUserKey, cancellationToken);
            if (member != null)
            {
                mentorMembers.Add(member);
            }
        }

        var uniqueMentors = mentorMembers
            .GroupBy(m => m.MemberKey)
            .Select(g => g.First())
            .ToList();

        var response = uniqueMentors.Select(mentor => mentor.ToLookupDto(_links)).ToList();
        return new ServiceResult<IEnumerable<MemberLookupDto>>(ResultType.Success, response);
    }
}
