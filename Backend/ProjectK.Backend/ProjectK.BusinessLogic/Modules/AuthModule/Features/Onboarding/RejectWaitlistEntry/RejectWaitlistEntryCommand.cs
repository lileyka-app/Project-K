using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.RejectWaitlistEntry;

public record RejectWaitlistEntryCommand(Guid WaitlistEntryKey, string? Note) : IRequest<ServiceResult<Guid>>;
