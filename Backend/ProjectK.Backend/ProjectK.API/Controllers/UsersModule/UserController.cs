using System.Collections.Generic;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Mfa;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.Account;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.TileLayout;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.User;
using ProjectK.BusinessLogic.Modules.UsersModule.Models;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos.UsersModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.UsersModule;

/// <summary>
/// Accounts as opposed to members: sign-in identity, account security, and the per-account settings the
/// app remembers. A member is a person in the kurin; a user is a way of signing in, and one may exist
/// without the other.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;
    public UserController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists every account. Administrators only.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpGet("users")]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers()
    {
        var request = new GetAllUsersQuery();
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns the caller's own account settings.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AccountSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAccountSettings()
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var response = await _mediator.Send(new GetAccountSettingsQuery(userKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Changes the caller's own account details.
    /// </summary>
    /// <remarks>
    /// Changing the email address does not take effect here — it is confirmed from the address itself at
    /// <see cref="ConfirmAccountEmailChange"/>.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPut("me")]
    [ProducesResponseType(typeof(AccountSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AccountSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAccountProfile([FromBody] UpdateAccountProfileRequestDto request)
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var response = await _mediator.Send(new UpdateAccountProfileCommand(userKey, request.Email, request.PhoneNumber, request.CurrentPassword));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Completes an email change using the token sent to the new address.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("me/email/confirm")]
    [ProducesResponseType(typeof(AccountSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AccountSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmAccountEmailChange([FromBody] ConfirmAccountEmailChangeRequestDto request)
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var response = await _mediator.Send(new ConfirmAccountEmailChangeCommand(userKey, request.Email, request.Token));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Changes the caller's own password, given the current one.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("me/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var response = await _mediator.Send(new ChangeOwnPasswordCommand(userKey, request.CurrentPassword, request.NewPassword));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Replaces the caller's own second factor, given a recovery code.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("me/mfa/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResetMfa([FromBody] ResetMfaRequestDto request)
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var response = await _mediator.Send(new ResetOwnMfaCommand(userKey, request.CurrentPassword));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Turns the caller's own second factor off.
    /// </summary>
    /// <remarks>
    /// Refused while an office the caller holds requires it.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("me/mfa/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DisableMfa([FromBody] DisableMfaRequestDto request)
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var response = await _mediator.Send(new DisableOwnMfaCommand(userKey, request.CurrentPassword));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns the caller's saved dashboard layouts.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("me/layouts")]
    [ProducesResponseType(typeof(IReadOnlyList<TileLayoutDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTileLayouts()
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var response = await _mediator.Send(new GetTileLayoutsQuery(userKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Saves the caller's layout for one board.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("me/layouts/{boardKey}")]
    [ProducesResponseType(typeof(TileLayoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveTileLayout(string boardKey, [FromBody] SaveTileLayoutRequestDto request)
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var tileKeys = request.TileKeys ?? new List<string>();
        var hiddenKeys = request.HiddenTileKeys ?? new List<string>();
        var response = await _mediator.Send(new SaveTileLayoutCommand(userKey, boardKey, tileKeys, hiddenKeys, request.SchemaVersion));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Discards the caller's layout for one board, restoring the default.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("me/layouts/{boardKey}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResetTileLayout(string boardKey)
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var response = await _mediator.Send(new ResetTileLayoutCommand(userKey, boardKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Clears somebody else's second factor so they can enrol again.
    /// </summary>
    /// <remarks>
    /// For the case the recovery codes are gone too. Refused when the account holds an office that requires
    /// the second factor, so the requirement cannot be lifted by asking for help.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("{userId:guid}/mfa/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetUserMfa(Guid userId)
    {
        var response = await _mediator.Send(new ResetUserMfaCommand(userId));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Suspends an account: it is refused at sign-in and every session it holds is ended. Administrators only.
    /// </summary>
    /// <remarks>
    /// Reversible, unlike deletion: the person, their memberships and everything they earned stay as they are.
    /// An administrator cannot suspend their own account.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("{userId:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendUser(Guid userId)
    {
        var response = await _mediator.Send(new SuspendUserCommand(userId));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Lets a suspended account sign in again. Administrators only.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("{userId:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreUser(Guid userId)
    {
        var response = await _mediator.Send(new RestoreUserCommand(userId));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Deletes an account. Administrators only.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var request = new DeleteUserCommand(userId);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Changes an account's role.
    /// </summary>
    /// <remarks>
    /// Only the account-level distinction between an administrator and everyone else. What a person may do
    /// inside a kurin follows from the offices they hold, which are changed through the leadership
    /// endpoints, not here.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpPost("{userId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeUserRole(Guid userId, [FromBody] UserRole newRole)
    {
        if (!Enum.IsDefined(newRole))
        {
            return this.Failure(ResultType.BadRequest, "InvalidRole", "Unknown user role.");
        }

        var request = new ChangeUserRoleCommand(userId, newRole);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }
}
