using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment.Revoke;

public record RevokeMentorCommand(Guid MentorUserKey, Guid GroupKey) : IRequest<ServiceResult<bool>>;
