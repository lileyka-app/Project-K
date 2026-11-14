using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.DevTools;

/// <summary>
/// Signs the administrator in as somebody who holds <paramref name="Role"/> in the kurin, with a
/// ticket that brings them back. A testing tool: it exists only on the local tiers, where the
/// controller that sends it is part of the application at all.
/// </summary>
public record ImpersonateRoleCommand(DevRole Role, Guid? KurinKey) : IRequest<ServiceResult<DevImpersonationDto>>;

public class ImpersonateRoleCommandHandler : IRequestHandler<ImpersonateRoleCommand, ServiceResult<DevImpersonationDto>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IMembershipDirectory _memberships;
    private readonly ILoginResponseFactory _loginResponseFactory;
    private readonly IJwtService _jwtService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActivityLogger _activityLogger;

    public ImpersonateRoleCommandHandler(
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        IMembershipDirectory memberships,
        ILoginResponseFactory loginResponseFactory,
        IJwtService jwtService,
        ICurrentUserContext currentUser,
        IActivityLogger activityLogger)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _members = members;
        _memberships = memberships;
        _loginResponseFactory = loginResponseFactory;
        _jwtService = jwtService;
        _currentUser = currentUser;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<DevImpersonationDto>> Handle(ImpersonateRoleCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } actorKey || !_currentUser.IsAdmin())
        {
            return ServiceResult<DevImpersonationDto>.Failure(ResultType.Forbidden, AuthErrors.AdminOnly, "Only an administrator can step into another role.");
        }

        // The kurin the admin is looking at, else the demo kurin: the tool is for testing a
        // feature quickly, and "the kurin on screen" is what a tester means.
        var kurinKey = request.KurinKey ?? _currentUser.KurinKey;
        if (kurinKey is null)
        {
            var demoKurin = await _unitOfWork.Kurins.GetByNumberAsync(Krai.Ukraine, 1, cancellationToken);
            kurinKey = demoKurin?.KurinKey;
        }

        if (kurinKey is null)
        {
            return ServiceResult<DevImpersonationDto>.Failure(ResultType.NotFound, AuthErrors.NoKurin, "There is no kurin to step into.");
        }

        var target = await new DemoSeatLocator(_userManager, _unitOfWork, _members, _memberships)
            .FindAsync(request.Role, kurinKey.Value, actorKey, cancellationToken);
        if (target is null)
        {
            return ServiceResult<DevImpersonationDto>.Failure(
                ResultType.NotFound,
                AuthErrors.NoAccountForRole,
                $"Nobody in this kurin holds {request.Role} with an account to sign in as.");
        }

        // Land them in this kurin even when the person belongs to several.
        target.ActiveKurinKey = kurinKey;
        await _userManager.UpdateAsync(target);

        var login = await _loginResponseFactory.CreateAsync(target, cancellationToken);
        var ticket = _jwtService.GenerateDevReturnTicket(actorKey);

        _activityLogger.LogAudit(
            action: "Dev.Impersonate",
            actorUserId: actorKey,
            targetUserId: target.Id,
            reason: $"Dev role switcher: {request.Role} in kurin {kurinKey}.");

        return new ServiceResult<DevImpersonationDto>(
            ResultType.Success,
            new DevImpersonationDto(login, ticket, request.Role.ToString(), kurinKey.Value));
    }
}
