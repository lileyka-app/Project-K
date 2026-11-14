using MediatR;
using ProjectK.Common.Models.Dtos.UsersModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.Account.Get;

public record GetAccountSettingsQuery(Guid UserKey) : IRequest<ServiceResult<AccountSettingsDto>>;
