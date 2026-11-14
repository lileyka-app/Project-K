using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.DevTools;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Demo;

/// <summary>
/// Signs a visitor into the demo kurin as whoever holds <paramref name="Seat"/> there. No password:
/// the data is a fixture that is put back every night, and the controller that sends this exists
/// only in the Demo environment.
/// </summary>
public sealed record EnterDemoCommand(DemoSeat Seat) : IRequest<ServiceResult<LoginUserDto>>;

public sealed class EnterDemoCommandHandler : IRequestHandler<EnterDemoCommand, ServiceResult<LoginUserDto>>
{
    /// <summary>The only environment where a password-less sign-in is a feature rather than a hole.</summary>
    public const string Environment = "Demo";

    /// <summary>The demo kurin is the one the seeder numbers first.</summary>
    private const int DemoKurinNumber = 1;

    private readonly IHostEnvironment _environment;
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IMembershipDirectory _memberships;
    private readonly ILoginResponseFactory _loginResponseFactory;
    private readonly IActivityLogger _activityLogger;

    public EnterDemoCommandHandler(
        IHostEnvironment environment,
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        IMembershipDirectory memberships,
        ILoginResponseFactory loginResponseFactory,
        IActivityLogger activityLogger)
    {
        _environment = environment;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _members = members;
        _memberships = memberships;
        _loginResponseFactory = loginResponseFactory;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<LoginUserDto>> Handle(EnterDemoCommand request, CancellationToken cancellationToken)
    {
        // The controller is absent outside Demo; this is the second line, for a host built without
        // that provider.
        if (!string.Equals(_environment.EnvironmentName, Environment, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<LoginUserDto>.Failure(ResultType.NotFound, AuthErrors.NotDemo, "There is no demo here.");
        }

        var kurin = await _unitOfWork.Kurins.GetByNumberAsync(Krai.Ukraine, DemoKurinNumber, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<LoginUserDto>.Failure(ResultType.NotFound, AuthErrors.NoDemoKurin, "The demo kurin is not seeded.");
        }

        var role = request.Seat switch
        {
            DemoSeat.Zvyazkovyi => DevRole.Zvyazkovyi,
            DemoSeat.Vykhovnyk => DevRole.Vykhovnyk,
            DemoSeat.Youth => DevRole.Member,
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };

        var locator = new DemoSeatLocator(_userManager, _unitOfWork, _members, _memberships);
        var account = await locator.FindAsync(role, kurin.KurinKey, excludeAccountKey: null, cancellationToken);
        if (account is null)
        {
            return ServiceResult<LoginUserDto>.Failure(ResultType.NotFound, AuthErrors.NoAccountForSeat, $"Nobody in the demo kurin holds {request.Seat}.");
        }

        account.ActiveKurinKey = kurin.KurinKey;
        await _userManager.UpdateAsync(account);

        var login = await _loginResponseFactory.CreateAsync(account, cancellationToken);

        _activityLogger.LogAudit(
            action: "Demo.Enter",
            targetUserId: account.Id,
            reason: $"Demo seat {request.Seat} in kurin {kurin.KurinKey}.");

        return new ServiceResult<LoginUserDto>(ResultType.Success, login);
    }
}
