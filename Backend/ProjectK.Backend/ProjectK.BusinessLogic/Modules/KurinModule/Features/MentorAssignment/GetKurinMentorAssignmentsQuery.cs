using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment;

public sealed record GetKurinMentorAssignmentsQuery(Guid KurinKey) : IRequest<ServiceResult<IEnumerable<MentorAssignmentDto>>>;

public sealed class GetKurinMentorAssignmentsQueryHandler : IRequestHandler<GetKurinMentorAssignmentsQuery, ServiceResult<IEnumerable<MentorAssignmentDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipDirectory _memberships;

    private readonly IBlobReadLinks _links;
    private readonly IKurinDirectory _kurins;

    public GetKurinMentorAssignmentsQueryHandler(IUnitOfWork unitOfWork, IMembershipDirectory memberships, IBlobReadLinks links, IKurinDirectory kurins)
    {
        _unitOfWork = unitOfWork;
        _memberships = memberships;
        _links = links;
        _kurins = kurins;
    }

    public async Task<ServiceResult<IEnumerable<MentorAssignmentDto>>> Handle(GetKurinMentorAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var assignments = await _unitOfWork.MentorAssignments.GetByKurinKeyAsync(request.KurinKey, cancellationToken);
        var memberLookup = (await _memberships.GetLookupByKurinAsync(request.KurinKey, cancellationToken))
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
