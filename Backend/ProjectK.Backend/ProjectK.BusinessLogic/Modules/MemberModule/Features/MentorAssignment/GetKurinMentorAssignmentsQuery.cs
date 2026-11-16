using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.MentorAssignment;

public record GetKurinMentorAssignmentsQuery(Guid KurinKey) : IRequest<ServiceResult<IEnumerable<MentorAssignmentDto>>>;

public class GetKurinMentorAssignmentsQueryHandler : IRequestHandler<GetKurinMentorAssignmentsQuery, ServiceResult<IEnumerable<MentorAssignmentDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;

    private readonly IBlobReadLinks _links;
    private readonly IKurinDirectory _kurins;

    public GetKurinMentorAssignmentsQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members, IBlobReadLinks links, IKurinDirectory kurins)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _links = links;
        _kurins = kurins;
    }

    public async Task<ServiceResult<IEnumerable<MentorAssignmentDto>>> Handle(GetKurinMentorAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var assignments = await _unitOfWork.MentorAssignments.GetByKurinKeyAsync(request.KurinKey, cancellationToken);
        var memberLookup = (await _members.GetLookupByKurinAsync(request.KurinKey, cancellationToken))
            .WithPhotoLinks(_links)
            .Where(member => member.UserKey.HasValue)
            .ToDictionary(member => member.UserKey!.Value);
        var groupNames = await _kurins.GetGroupNamesAsync(request.KurinKey, cancellationToken);
        var response = new List<MentorAssignmentDto>();

        foreach (var assignment in assignments.OrderByDescending(a => a.AssignedAtUtc))
        {
            response.Add(new MentorAssignmentDto
            {
                MentorAssignmentKey = assignment.MentorAssignmentKey,
                MentorUserKey = assignment.MentorUserKey,
                GroupKey = assignment.GroupKey,
                GroupName = groupNames.GetValueOrDefault(assignment.GroupKey, string.Empty),
                AssignedAtUtc = assignment.AssignedAtUtc,
                RevokedAtUtc = assignment.RevokedAtUtc,
                Member = memberLookup.GetValueOrDefault(assignment.MentorUserKey)
            });
        }

        return new ServiceResult<IEnumerable<MentorAssignmentDto>>(ResultType.Success, response);
    }
}
