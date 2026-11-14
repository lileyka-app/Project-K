using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.DevTools;

/// <summary>
/// Signs the administrator in as one particular person: the one whose card is open. The sibling of
/// <c>ImpersonateRoleCommand</c>, for when the tester wants "this member", not "somebody with that office".
/// </summary>
public record ImpersonateMemberCommand(Guid MemberKey) : IRequest<ServiceResult<DevImpersonationDto>>;

public class ImpersonateMemberCommandHandler : IRequestHandler<ImpersonateMemberCommand, ServiceResult<DevImpersonationDto>>
{
    /// <summary>What the answer calls the seat when it is a named person rather than an office.</summary>
    public const string PersonRole = "Person";

    private readonly UserManager<AppUser> _userManager;
    private readonly IMemberDirectory _members;
    private readonly IMembershipDirectory _memberships;
    private readonly ILoginResponseFactory _loginResponseFactory;
    private readonly IJwtService _jwtService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActivityLogger _activityLogger;

    public ImpersonateMemberCommandHandler(
        UserManager<AppUser> userManager,
        IMemberDirectory members,
        IMembershipDirectory memberships,
        ILoginResponseFactory loginResponseFactory,
        IJwtService jwtService,
        ICurrentUserContext currentUser,
        IActivityLogger activityLogger)
    {
        _userManager = userManager;
        _members = members;
        _memberships = memberships;
        _loginResponseFactory = loginResponseFactory;
        _jwtService = jwtService;
        _currentUser = currentUser;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<DevImpersonationDto>> Handle(ImpersonateMemberCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } actorKey || !_currentUser.IsAdmin())
        {
            return ServiceResult<DevImpersonationDto>.Failure(ResultType.Forbidden, AuthErrors.AdminOnly, "Only an administrator can step into another account.");
        }

        var person = await _members.FindAsync(request.MemberKey, cancellationToken);
        if (person is null)
        {
            return ServiceResult<DevImpersonationDto>.Failure(ResultType.NotFound, AuthErrors.NoSuchMember, "There is no such member.");
        }

        var target = person.UserKey is { } accountKey ? await _userManager.FindByIdAsync(accountKey.ToString()) : null;
        if (target is null || !target.CanSignIn())
        {
            return ServiceResult<DevImpersonationDto>.Failure(ResultType.NotFound, AuthErrors.NoAccountForMember, $"{person.FullName} has no account to sign in as.");
        }

        if (target.Id == actorKey || await _userManager.IsInRoleAsync(target, SystemRole.Admin))
        {
            return ServiceResult<DevImpersonationDto>.Failure(ResultType.Conflict, AuthErrors.AdminAccount, "That account is an administrator already.");
        }

        // Land where the tester is looking when the person stands there too; otherwise in the
        // person's own kurin, so the borrowed session opens on something rather than on a chooser.
        var kurins = await _memberships.GetKurinKeysForAccountAsync(target.Id, cancellationToken);
        var kurinKey = _currentUser.KurinKey is { } onScreen && kurins.Contains(onScreen)
            ? onScreen
            : kurins.FirstOrDefault(person.KurinKey);

        target.ActiveKurinKey = kurinKey;
        await _userManager.UpdateAsync(target);

        var login = await _loginResponseFactory.CreateAsync(target, cancellationToken);
        var ticket = _jwtService.GenerateDevReturnTicket(actorKey);

        _activityLogger.LogAudit(
            action: "Dev.Impersonate",
            actorUserId: actorKey,
            targetUserId: target.Id,
            reason: $"Dev role switcher: member {person.MemberKey} in kurin {kurinKey}.");

        return new ServiceResult<DevImpersonationDto>(
            ResultType.Success,
            new DevImpersonationDto(login, ticket, PersonRole, kurinKey));
    }
}
