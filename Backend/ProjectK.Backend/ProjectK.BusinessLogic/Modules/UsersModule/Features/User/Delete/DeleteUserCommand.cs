using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.User.Delete;

public record DeleteUserCommand(Guid UserId) : IRequest<ServiceResult<bool>>;
