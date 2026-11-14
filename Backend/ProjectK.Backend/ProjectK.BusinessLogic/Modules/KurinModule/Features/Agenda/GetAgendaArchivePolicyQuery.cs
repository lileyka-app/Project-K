using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

/// <summary>A kurin's archive rules, for its settings page.</summary>
public sealed record GetAgendaArchivePolicyQuery(Guid KurinKey) : IRequest<ServiceResult<AgendaArchivePolicyDto>>;

public sealed class GetAgendaArchivePolicyQueryHandler : IRequestHandler<GetAgendaArchivePolicyQuery, ServiceResult<AgendaArchivePolicyDto>>
{
    private readonly IUnitOfWork _uow;

    public GetAgendaArchivePolicyQueryHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ServiceResult<AgendaArchivePolicyDto>> Handle(GetAgendaArchivePolicyQuery request, CancellationToken cancellationToken)
    {
        var kurin = await _uow.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        return kurin is null
            ? ServiceResult<AgendaArchivePolicyDto>.Failure(ResultType.NotFound, AgendaErrors.KurinNotFound, "Kurin was not found.")
            : new ServiceResult<AgendaArchivePolicyDto>(ResultType.Success,
                new AgendaArchivePolicyDto { AutoArchiveAfterDays = kurin.TaskAutoArchiveAfterDays, PurgeAfterDays = kurin.TaskArchivePurgeAfterDays });
    }
}
