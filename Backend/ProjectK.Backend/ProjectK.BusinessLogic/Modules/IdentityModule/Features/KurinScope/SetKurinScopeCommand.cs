using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.KurinScope;

/// <param name="KurinKey">Kurin to step into, or null to return to system-wide scope.</param>
public sealed record SetKurinScopeCommand(Guid UserKey, Guid? KurinKey)
    : IRequest<ServiceResult<LoginUserDto>>;

public sealed class SetKurinScopeCommandValidator : AbstractValidator<SetKurinScopeCommand>
{
    public SetKurinScopeCommandValidator()
    {
        // KurinKey is intentionally optional: null means "return to system-wide scope".
        RuleFor(command => command.UserKey).NotEmpty();
    }
}

public sealed class SetKurinScopeCommandHandler : IRequestHandler<SetKurinScopeCommand, ServiceResult<LoginUserDto>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILoginResponseService _loginResponseService;
    private readonly IMembershipDirectory _memberships;

    public SetKurinScopeCommandHandler(
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        ILoginResponseService loginResponseService,
        IMembershipDirectory memberships)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _loginResponseService = loginResponseService;
        _memberships = memberships;
    }

    public async Task<ServiceResult<LoginUserDto>> Handle(
        SetKurinScopeCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindSignedInAsync(request.UserKey);
        if (user is null)
        {
            return IdentityErrors.AccountMissing<LoginUserDto>();
        }

        // Switching kurin is no longer an admin's privilege — it is what someone does when they
        // belong to more than one. An admin steps into any kurin, or out of all of them; everyone
        // else may only stand where they actually are, which is what their memberships say.
        var isAdmin = await _userManager.IsInRoleAsync(user, SystemRoleNames.Admin);

        if (request.KurinKey is null)
        {
            if (!isAdmin)
            {
                return ServiceResult<LoginUserDto>.Failure(
                    ResultType.Forbidden,
                    IdentityErrors.KurinScopeForbidden,
                    "Only an admin can be outside every kurin.");
            }
        }
        else
        {
            var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey.Value, cancellationToken);
            if (kurin is null)
            {
                return ServiceResult<LoginUserDto>.Failure(
                    ResultType.NotFound,
                    IdentityErrors.KurinNotFound,
                    "Kurin to scope into was not found.");
            }

            if (!isAdmin)
            {
                var belongsTo = await _memberships.GetKurinKeysForAccountAsync(user.Id, cancellationToken);
                if (!belongsTo.Contains(request.KurinKey.Value))
                {
                    return ServiceResult<LoginUserDto>.Failure(
                        ResultType.Forbidden,
                        IdentityErrors.KurinScopeForbidden,
                        "You have no current membership in that kurin.");
                }
            }
        }

        user.ActiveKurinKey = request.KurinKey;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return ServiceResult<LoginUserDto>.Failure(
                ResultType.BadRequest,
                IdentityErrors.KurinScopeNotSaved,
                "Kurin scope could not be saved.");
        }

        var response = await _loginResponseService.CreateAsync(user, cancellationToken);
        return new ServiceResult<LoginUserDto>(ResultType.Success, response);
    }
}
