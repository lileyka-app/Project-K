using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AuthModule;

/// <summary>
/// The <c>error</c> codes the auth module answers with. The sign-in, forgot-password and MFA codes
/// are shared on purpose: several failures answer with one code so that a caller cannot tell an
/// unknown address from a wrong password. Keep it that way when adding a case.
/// </summary>
public static class AuthErrors
{
    // Sessions and credentials
    public const string Unauthorized = "Unauthorized";
    public const string InvalidToken = "InvalidToken";
    public const string InvalidCredentials = "InvalidCredentials";
    public const string TooManyAttempts = "TooManyAttempts";
    public const string UserNotFound = "UserNotFound";
    public const string InvalidApiKey = "InvalidApiKey";
    public const string AccountNotSaved = "AccountNotSaved";

    // Second factor
    public const string InvalidCode = "InvalidCode";
    public const string MfaSetupFailed = "MfaSetupFailed";
    public const string MfaNotEnabled = "MfaNotEnabled";
    public const string MfaRequired = "MfaRequired";
    public const string MfaDisableFailed = "MfaDisableFailed";
    public const string MfaResetFailed = "MfaResetFailed";
    public const string CannotResetPrivilegedMfa = "CannotResetPrivilegedMfa";

    /// <summary>A privileged account without a second factor tried to change something.</summary>
    public const string PrivilegedMfaRequired = "PrivilegedMfaRequired";

    // Access checks and kurin scope
    public const string Forbidden = "Forbidden";
    public const string InvalidEntityKey = "InvalidEntityKey";
    public const string InvalidEntityType = "InvalidEntityType";
    public const string InvalidResourceAction = "InvalidResourceAction";
    public const string KurinNotFound = "KurinNotFound";
    public const string KurinScopeForbidden = "KurinScopeForbidden";
    public const string KurinScopeNotSaved = "KurinScopeNotSaved";

    // Demo and dev tools
    public const string NotDemo = "NotDemo";
    public const string NoDemoKurin = "NoDemoKurin";
    public const string NoAccountForSeat = "NoAccountForSeat";
    public const string AdminOnly = "AdminOnly";
    public const string NoKurin = "NoKurin";
    public const string NoAccountForRole = "NoAccountForRole";
    public const string NoSuchMember = "NoSuchMember";
    public const string NoAccountForMember = "NoAccountForMember";
    public const string AdminAccount = "AdminAccount";
    public const string InvalidTicket = "InvalidTicket";
    public const string NotAnAdministrator = "NotAnAdministrator";

    // Onboarding and accounts
    public const string InvalidInvitationToken = "InvalidInvitationToken";
    public const string InvitationHasNoUser = "InvitationHasNoUser";
    public const string PasswordNotSet = "PasswordNotSet";
    public const string UserNotUpdated = "UserNotUpdated";
    public const string UserNotCreated = "UserNotCreated";
    public const string WaitlistEntryNotFound = "WaitlistEntryNotFound";
    public const string WaitlistEntryAlreadyApproved = "WaitlistEntryAlreadyApproved";
    public const string WaitlistEntryExists = "WaitlistEntryExists";
    public const string WaitlistPending = "WaitlistPending";
    public const string MemberEmailExists = "MemberEmailExists";
    public const string EmailTaken = "EmailTaken";
    public const string EmailNotChanged = "EmailNotChanged";
    public const string AccountNotFound = "AccountNotFound";
    public const string AccountNotPending = "AccountNotPending";
    public const string KraiNotFound = "KraiNotFound";
    public const string KurinNumberTaken = "KurinNumberTaken";
    public const string InvalidResetAttempt = "InvalidResetAttempt";

    // Setup
    public const string AlreadyInitialized = "AlreadyInitialized";
    public const string SetupFailed = "SetupFailed";

    /// <summary>
    /// The signed-in account could not be found. Answers as an expired sign-in rather than a missing
    /// record: the caller holds a token for an account that is not there.
    /// </summary>
    public static ServiceResult<T> AccountMissing<T>() =>
        ServiceResult<T>.Failure(ResultType.Unauthorized, Unauthorized, "User not found or unauthorized.");

    /// <summary>
    /// A kept session is gone, spent or belongs to an account that may not sign in. One answer for
    /// all of them, so a session cannot be probed for which it was.
    /// </summary>
    public static ServiceResult<T> SessionEnded<T>() =>
        ServiceResult<T>.Failure(ResultType.Unauthorized, Unauthorized, "Authentication is required or the credentials are invalid.");
}
