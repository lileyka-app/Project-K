using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.KurinScope;

/// <param name="KurinKey">Kurin to step into, or null to return to system-wide scope.</param>
public record SetKurinScopeCommand(Guid UserKey, Guid? KurinKey)
    : IRequest<ServiceResult<LoginUserDto>>;

public sealed class SetKurinScopeCommandValidator : AbstractValidator<SetKurinScopeCommand>
{
    public SetKurinScopeCommandValidator()
    {
        // KurinKey is intentionally optional: null means "return to system-wide scope".
        RuleFor(command => command.UserKey).NotEmpty();
    }
}

public class SetKurinScopeCommandHandler : IRequestHandler<SetKurinScopeCommand, ServiceResult<LoginUserDto>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILoginResponseFactory _loginResponseFactory;
    private readonly IMembershipDirectory _memberships;

    public SetKurinScopeCommandHandler(
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        ILoginResponseFactory loginResponseFactory,
        IMembershipDirectory memberships)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _loginResponseFactory = loginResponseFactory;
        _memberships = memberships;
    }

    public async Task<ServiceResult<LoginUserDto>> Handle(
        SetKurinScopeCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserKey.ToString());
        if (user is null)
        {
            return AuthErrors.AccountMissing<LoginUserDto>();
        }

        // Switching kurin is no longer an admin's privilege — it is what someone does when they
        // belong to more than one. An admin steps into any kurin, or out of all of them; everyone
        // else may only stand where they actually are, which is what their memberships say.
        var isAdmin = await _userManager.IsInRoleAsync(user, SystemRole.Admin);

        if (request.KurinKey is null)
        {
            if (!isAdmin)
            {
                return ServiceResult<LoginUserDto>.Failure(
                    ResultType.Forbidden,
                    AuthErrors.KurinScopeForbidden,
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
                    AuthErrors.KurinNotFound,
                    "Kurin to scope into was not found.");
            }

            if (!isAdmin)
            {
                var belongsTo = await _memberships.GetKurinKeysForAccountAsync(user.Id, cancellationToken);
                if (!belongsTo.Contains(request.KurinKey.Value))
                {
                    return ServiceResult<LoginUserDto>.Failure(
                        ResultType.Forbidden,
                        AuthErrors.KurinScopeForbidden,
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
                AuthErrors.KurinScopeNotSaved,
                "Kurin scope could not be saved.");
        }

        var response = await _loginResponseFactory.CreateAsync(user, cancellationToken);
        return new ServiceResult<LoginUserDto>(ResultType.Success, response);
    }
}
