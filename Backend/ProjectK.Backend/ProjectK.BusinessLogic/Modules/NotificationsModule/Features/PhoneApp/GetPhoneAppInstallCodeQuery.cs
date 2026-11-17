using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.Options;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Options.NotificationsModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Features.PhoneApp;

/// <summary>
/// The QR code a computer shows for «Встановити на телефон». It points at the phone app's install
/// page on the site's public address, the same one the letters link to, so a self-hosted instance
/// points at itself as long as its letters do.
/// </summary>
public sealed record GetPhoneAppInstallCodeQuery : IRequest<ServiceResult<PhoneAppInstallCodeDto>>;

public sealed class GetPhoneAppInstallCodeQueryHandler : IRequestHandler<GetPhoneAppInstallCodeQuery, ServiceResult<PhoneAppInstallCodeDto>>
{
    private const string InstallPath = "/m/install";

    /// <summary>
    /// The code for each address, drawn once per process. The address is the site's own and holds
    /// nothing about the person asking, so one picture serves everyone until the next start.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> Drawn = new(StringComparer.Ordinal);

    private readonly IQrCodeService _qrCodes;
    private readonly EmailOptions _site;

    public GetPhoneAppInstallCodeQueryHandler(IQrCodeService qrCodes, IOptions<EmailOptions> site)
    {
        _qrCodes = qrCodes;
        _site = site.Value;
    }

    public Task<ServiceResult<PhoneAppInstallCodeDto>> Handle(GetPhoneAppInstallCodeQuery request, CancellationToken cancellationToken)
    {
        var url = $"{_site.BaseUrl.TrimEnd('/')}{InstallPath}";
        var code = new PhoneAppInstallCodeDto { Url = url, QrCode = Drawn.GetOrAdd(url, _qrCodes.ToPngDataUrl) };
        return Task.FromResult(new ServiceResult<PhoneAppInstallCodeDto>(ResultType.Success, code));
    }
}
