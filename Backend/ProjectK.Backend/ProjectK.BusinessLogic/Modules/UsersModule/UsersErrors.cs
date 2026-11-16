using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.UsersModule;

/// <summary>The <c>error</c> codes the users module answers with: accounts, their settings and the admin list.</summary>
public static class UsersErrors
{
    /// <summary>The signed-in account is gone; answered through <see cref="AccountMissing{T}"/>.</summary>
    public const string Unauthorized = "Unauthorized";
    public const string UserNotFound = "UserNotFound";
    public const string InvalidEmail = "InvalidEmail";
    public const string EmailAlreadyInUse = "EmailAlreadyInUse";
    public const string InvalidCredentials = "InvalidCredentials";
    public const string InvalidToken = "InvalidToken";
    public const string UpdateFailed = "UpdateFailed";
    public const string PasswordChangeFailed = "PasswordChangeFailed";
    public const string LayoutTooLarge = "LayoutTooLarge";
    /// <summary>The account role asked for is not one the system knows.</summary>
    public const string InvalidRole = "InvalidRole";
    public const string AdminOnly = "AdminOnly";
    public const string AdminRoleNotGranted = "AdminRoleNotGranted";
    public const string AdminRoleNotRevoked = "AdminRoleNotRevoked";
    public const string UserNotDeleted = "UserNotDeleted";
    public const string CannotSuspendSelf = "CannotSuspendSelf";
    public const string SuspendFailed = "SuspendFailed";
    public const string RestoreFailed = "RestoreFailed";

    /// <summary>The account an administrator acts on does not exist.</summary>
    public static ServiceResult<T> TargetNotFound<T>() =>
        ServiceResult<T>.Failure(ResultType.NotFound, UserNotFound, "Target user not found.");

    /// <summary>
    /// The signed-in account could not be found. Answers as an expired sign-in rather than a missing
    /// record: the caller holds a token for an account that is not there.
    /// </summary>
    public static ServiceResult<T> AccountMissing<T>() =>
        ServiceResult<T>.Failure(ResultType.Unauthorized, Unauthorized, "User not found or unauthorized.");
}
