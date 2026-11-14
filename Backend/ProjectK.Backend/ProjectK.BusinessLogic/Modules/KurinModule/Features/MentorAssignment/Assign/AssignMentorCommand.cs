using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment.Assign;

public record AssignMentorCommand(Guid MentorUserKey, Guid GroupKey) : IRequest<ServiceResult<Guid>>;
