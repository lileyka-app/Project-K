using AutoMapper;
using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment;

public record GetGroupMentorsQuery(Guid GroupKey) : IRequest<ServiceResult<IEnumerable<MemberLookupDto>>>;

public class GetGroupMentorsQueryHandler : IRequestHandler<GetGroupMentorsQuery, ServiceResult<IEnumerable<MemberLookupDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IMapper _mapper;

    public GetGroupMentorsQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _mapper = mapper;
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

        var response = _mapper.Map<IEnumerable<MemberLookupDto>>(uniqueMentors);
        return new ServiceResult<IEnumerable<MemberLookupDto>>(ResultType.Success, response);
    }
}
