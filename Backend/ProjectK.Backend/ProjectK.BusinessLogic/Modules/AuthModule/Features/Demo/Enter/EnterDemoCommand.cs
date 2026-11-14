using MediatR;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Demo.Enter;

/// <summary>
/// Signs a visitor into the demo kurin as whoever holds <paramref name="Seat"/> there. No password:
/// the data is a fixture that is put back every night, and the controller that sends this exists
/// only in the Demo environment.
/// </summary>
public sealed record EnterDemoCommand(DemoSeat Seat) : IRequest<ServiceResult<LoginUserResponse>>;
