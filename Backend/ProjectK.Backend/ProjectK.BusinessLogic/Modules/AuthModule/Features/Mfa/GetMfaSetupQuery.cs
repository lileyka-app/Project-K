using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Mfa;

public record GetMfaSetupQuery(Guid UserKey) : IRequest<ServiceResult<MfaSetupResponseDto>>;

public class GetMfaSetupQueryHandler : IRequestHandler<GetMfaSetupQuery, ServiceResult<MfaSetupResponseDto>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IQrCodeService _qrCodes;
    private readonly UrlEncoder _urlEncoder;
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    public GetMfaSetupQueryHandler(UserManager<AppUser> userManager, IQrCodeService qrCodes, UrlEncoder urlEncoder)
    {
        _userManager = userManager;
        _qrCodes = qrCodes;
        _urlEncoder = urlEncoder;
    }

    public async Task<ServiceResult<MfaSetupResponseDto>> Handle(GetMfaSetupQuery request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserKey.ToString());
        if (user == null)
        {
            return ServiceResult<MfaSetupResponseDto>.Failure(ResultType.NotFound, AuthErrors.UserNotFound, "User not found.");
        }

        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = await _userManager.GetEmailAsync(user);
        var authenticatorUri = string.Format(
            AuthenticatorUriFormat,
            _urlEncoder.Encode("Project-K"),
            _urlEncoder.Encode(email!),
            unformattedKey);

        var qrCodeBase64 = _qrCodes.ToPngDataUrl(authenticatorUri);

        return new ServiceResult<MfaSetupResponseDto>(
            ResultType.Success,
            new MfaSetupResponseDto(unformattedKey!, authenticatorUri, qrCodeBase64));
    }
}
