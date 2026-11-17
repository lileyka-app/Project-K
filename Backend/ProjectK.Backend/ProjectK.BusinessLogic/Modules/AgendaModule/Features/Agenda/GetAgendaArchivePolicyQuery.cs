using MediatR;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;

/// <summary>A kurin's archive rules, for its settings page.</summary>
public sealed record GetAgendaArchivePolicyQuery(Guid KurinKey) : IRequest<ServiceResult<AgendaArchivePolicyDto>>;

public sealed class GetAgendaArchivePolicyQueryHandler : IRequestHandler<GetAgendaArchivePolicyQuery, ServiceResult<AgendaArchivePolicyDto>>
{
    private readonly IKurinDirectory _kurins;

    public GetAgendaArchivePolicyQueryHandler(IKurinDirectory kurins)
    {
        _kurins = kurins;
    }

    public async Task<ServiceResult<AgendaArchivePolicyDto>> Handle(GetAgendaArchivePolicyQuery request, CancellationToken cancellationToken)
    {
        var policy = await _kurins.GetTaskArchivePolicyAsync(request.KurinKey, cancellationToken);
        return policy is null
            ? ServiceResult<AgendaArchivePolicyDto>.Failure(ResultType.NotFound, AgendaErrors.KurinNotFound, "Kurin was not found.")
            : new ServiceResult<AgendaArchivePolicyDto>(ResultType.Success,
                new AgendaArchivePolicyDto { AutoArchiveAfterDays = policy.AutoArchiveAfterDays, PurgeAfterDays = policy.PurgeAfterDays });
    }
}
