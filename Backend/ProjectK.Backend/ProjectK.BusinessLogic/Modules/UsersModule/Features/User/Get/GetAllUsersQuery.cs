using MediatR;
using ProjectK.BusinessLogic.Modules.UsersModule.Models;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.User.Get;

public class GetAllUsersQuery : IRequest<ServiceResult<IEnumerable<UserDto>>>
{
}
