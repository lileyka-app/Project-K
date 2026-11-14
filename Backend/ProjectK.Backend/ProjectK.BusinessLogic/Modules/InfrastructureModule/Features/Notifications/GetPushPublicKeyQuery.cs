using MediatR;
using Microsoft.Extensions.Options;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Models.Settings;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Notifications;

/// <summary>The key a browser subscribes with; <c>null</c> when this server sends no pushes.</summary>
public sealed record GetPushPublicKeyQuery : IRequest<ServiceResult<WebPushPublicKeyDto>>;

public sealed class GetPushPublicKeyQueryHandler
    : IRequestHandler<GetPushPublicKeyQuery, ServiceResult<WebPushPublicKeyDto>>
{
    private readonly WebPushOptions _options;

    public GetPushPublicKeyQueryHandler(IOptions<WebPushOptions> options)
    {
        _options = options.Value;
    }

    public Task<ServiceResult<WebPushPublicKeyDto>> Handle(GetPushPublicKeyQuery request, CancellationToken cancellationToken)
    {
        var key = new WebPushPublicKeyDto { PublicKey = _options.IsConfigured ? _options.PublicKey!.Trim() : null };
        return Task.FromResult(new ServiceResult<WebPushPublicKeyDto>(ResultType.Success, key));
    }
}
