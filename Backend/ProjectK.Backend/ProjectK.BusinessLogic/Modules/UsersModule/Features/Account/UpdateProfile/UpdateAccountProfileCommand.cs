using MediatR;
using ProjectK.Common.Models.Dtos.UsersModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.Account.UpdateProfile;

public record UpdateAccountProfileCommand(Guid UserKey, string Email, string? PhoneNumber, string? CurrentPassword = null)
    : IRequest<ServiceResult<AccountSettingsDto>>;
