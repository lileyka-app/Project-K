using MediatR;
using Microsoft.Extensions.Options;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Options.KurinModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding;

/// <summary>The numbers a kurin may carry, so the forms can say so before the server refuses.</summary>
public sealed record GetKurinNumberRangeQuery : IRequest<ServiceResult<KurinNumberRange>>;

public sealed class GetKurinNumberRangeQueryHandler : IRequestHandler<GetKurinNumberRangeQuery, ServiceResult<KurinNumberRange>>
{
    private readonly KurinNumberOptions _numbers;

    public GetKurinNumberRangeQueryHandler(IOptions<KurinNumberOptions> numbers)
    {
        _numbers = numbers.Value;
    }

    public Task<ServiceResult<KurinNumberRange>> Handle(GetKurinNumberRangeQuery request, CancellationToken cancellationToken)
    {
        var range = new KurinNumberRange(KurinNumberOptions.MinNumber, _numbers.MaxNumber);
        return Task.FromResult(new ServiceResult<KurinNumberRange>(ResultType.Success, range));
    }
}
