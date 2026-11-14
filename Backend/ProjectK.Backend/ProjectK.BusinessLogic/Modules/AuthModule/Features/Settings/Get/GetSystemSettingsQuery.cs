using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Settings.Get;

public record GetSystemSettingsQuery() : IRequest<ServiceResult<Dictionary<string, string>>>;
