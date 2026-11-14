using MediatR;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.GetWaitlistEntries;

public record GetWaitlistEntriesQuery : IRequest<ServiceResult<IEnumerable<WaitlistEntry>>>;
