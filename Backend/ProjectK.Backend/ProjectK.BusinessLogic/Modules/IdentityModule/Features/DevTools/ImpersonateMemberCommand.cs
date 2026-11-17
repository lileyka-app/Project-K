using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.DevTools;

/// <summary>
/// Signs the administrator in as one particular person: the one whose card is open. The sibling of
/// <c>ImpersonateRoleCommand</c>, for when the tester wants "this member", not "somebody with that office".
/// </summary>
public sealed record ImpersonateMemberCommand(Guid MemberKey) : IRequest<ServiceResult<DevImpersonationDto>>;

public sealed class ImpersonateMemberCommandHandler : IRequestHandler<ImpersonateMemberCommand, ServiceResult<DevImpersonationDto>>
{
    /// <summary>What the answer calls the seat when it is a named person rather than an office.</summary>
    public const string PersonRole = "Person";

    private readonly UserManager<AppUser> _userManager;
    private readonly IMemberDirectory _members;
    private readonly IMembershipDirectory _memberships;
    private readonly ILoginResponseService _loginResponseService;
    private readonly IJwtService _jwtService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActivityLogger _activityLogger;

    public ImpersonateMemberCommandHandler(
        UserManager<AppUser> userManager,
        IMemberDirectory members,
        IMembershipDirectory memberships,
        ILoginResponseService loginResponseService,
        IJwtService jwtService,
        ICurrentUserContext currentUser,
        IActivityLogger activityLogger)
    {
        _userManager = userManager;
        _members = members;
        _memberships = memberships;
        _loginResponseService = loginResponseService;
        _jwtService = jwtService;
        _currentUser = currentUser;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<DevImpersonationDto>> Handle(ImpersonateMemberCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } actorKey || !_currentUser.IsAdmin())
        {
            return ServiceResult<DevImpersonationDto>.Failure(ResultType.Forbidden, IdentityErrors.AdminOnly, "Only an administrator can step into another account.");
        }

        var person = await _members.FindAsync(request.MemberKey, cancellationToken);
        if (person is null)
        {
            return ServiceResult<DevImpersonationDto>.Failure(ResultType.NotFound, IdentityErrors.NoSuchMember, "There is no such member.");
        }

        var target = person.UserKey is { } accountKey ? await _userManager.FindByIdAsync(accountKey.ToString()) : null;
        if (target is null || !target.CanSignIn())
        {
            return ServiceResult<DevImpersonationDto>.Failure(ResultType.NotFound, IdentityErrors.NoAccountForMember, $"{person.FullName} has no account to sign in as.");
        }

        if (target.Id == actorKey || await _userManager.IsInRoleAsync(target, SystemRoleNames.Admin))
        {
            return ServiceResult<DevImpersonationDto>.Failure(ResultType.Conflict, IdentityErrors.AdminAccount, "That account is an administrator already.");
        }

        // Land where the tester is looking when the person stands there too; otherwise in the
        // person's own kurin, so the borrowed session opens on something rather than on a chooser.
        var kurins = await _memberships.GetKurinKeysForAccountAsync(target.Id, cancellationToken);
        var kurinKey = _currentUser.KurinKey is { } onScreen && kurins.Contains(onScreen)
            ? onScreen
            : kurins.FirstOrDefault(person.KurinKey);

        target.ActiveKurinKey = kurinKey;
        await _userManager.UpdateAsync(target);

        var login = await _loginResponseService.CreateAsync(target, cancellationToken);
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
