using MediatR;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.ResendInvitation;

public record ResendInvitationCommand(Guid WaitlistEntryKey) : IRequest<ServiceResult<Guid>>, ITransactionalRequest;

public record ResendInvitationByEmailCommand(string Email) : IRequest<ServiceResult<bool>>, ITransactionalRequest;
