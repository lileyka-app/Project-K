using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.API.Controllers.AuthModule;
using ProjectK.API.Controllers.UsersModule;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Dtos.AuthModule.Requests;
using ProjectK.Common.Dtos.UsersModule;
using ProjectK.Common.Dtos.UsersModule.Requests;

namespace ProjectK.API.Tests.Security;

public class AccountSecurityRateLimitingTests
{
    [Theory]
    [MemberData(nameof(AccountSecurityEndpoints))]
    public void AccountSecurityEndpoint_ShouldUseAccountSecurityRateLimit(MethodInfo action)
    {
        var attribute = action.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("AccountSecurityLimit", attribute!.PolicyName);
    }

    public static IEnumerable<object[]> AccountSecurityEndpoints()
    {
        yield return Row<Action<AuthController>>(nameof(AuthController.GetMfaSetup));
        yield return Row<Action<AuthController, MfaVerifyRequest>>(nameof(AuthController.EnableMfa));
        yield return Row<Action<AuthController, MfaRecoveryCodesRequest>>(nameof(AuthController.RotateMfaRecoveryCodes));
        yield return Row<Action<AuthController, MfaLoginRequest, bool>>(nameof(AuthController.VerifyMfaLogin));
        yield return Row<Action<AuthController>>(nameof(AuthController.GetMfaStatus));

        yield return Row<Action<UserController>>(nameof(UserController.GetAccountSettings));
        yield return Row<Action<UserController, UpdateAccountProfileRequest>>(nameof(UserController.UpdateAccountProfile));
        yield return Row<Action<UserController, ConfirmAccountEmailChangeRequest>>(nameof(UserController.ConfirmAccountEmailChange));
        yield return Row<Action<UserController, ChangePasswordRequest>>(nameof(UserController.ChangePassword));
        yield return Row<Action<UserController, ResetMfaRequest>>(nameof(UserController.ResetMfa));
        yield return Row<Action<UserController, DisableMfaRequest>>(nameof(UserController.DisableMfa));
        yield return Row<Action<UserController, Guid>>(nameof(UserController.ResetUserMfa));

        yield return Row<Action<OnboardingController, RequestPasswordResetCommand>>(nameof(OnboardingController.RequestPasswordReset));
        yield return Row<Action<OnboardingController, ResetPasswordCommand>>(nameof(OnboardingController.ResetPassword));
    }

    private static object[] Row<TDelegate>(string methodName)
    {
        var invoke = typeof(TDelegate).GetMethod("Invoke")!;
        var parameters = invoke.GetParameters();
        var controllerType = parameters[0].ParameterType;
        var actionParameterTypes = parameters.Skip(1).Select(p => p.ParameterType).ToArray();
        var method = controllerType.GetMethod(methodName, actionParameterTypes);

        return [method ?? throw new InvalidOperationException($"Unable to resolve {controllerType.Name}.{methodName}.")];
    }
}
