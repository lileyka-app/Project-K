using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

/// <summary>Sets how long a kurin's done tasks stay on the board and its archive is kept. Gated as kurin settings.</summary>
public sealed record SetAgendaArchivePolicyCommand(Guid KurinKey, int? AutoArchiveAfterDays, int? PurgeAfterDays)
    : IRequest<ServiceResult<AgendaArchivePolicyDto>>;

public sealed class SetAgendaArchivePolicyCommandHandler : IRequestHandler<SetAgendaArchivePolicyCommand, ServiceResult<AgendaArchivePolicyDto>>
{
    /// <summary>What the kurin's journal calls this setting.</summary>
    public const string Label = "Архів задач";

    private readonly IUnitOfWork _uow;
    private readonly IKurinAudit _audit;

    public SetAgendaArchivePolicyCommandHandler(IUnitOfWork uow, IKurinAudit audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<ServiceResult<AgendaArchivePolicyDto>> Handle(SetAgendaArchivePolicyCommand request, CancellationToken cancellationToken)
    {
        if (request.AutoArchiveAfterDays is < 1 or > 3650 || request.PurgeAfterDays is < 1 or > 3650)
        {
            return ServiceResult<AgendaArchivePolicyDto>.Failure(ResultType.BadRequest, AgendaErrors.AgendaArchivePolicyRange, "A period is between 1 and 3650 days, or empty to switch it off.");
        }

        var kurin = await _uow.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<AgendaArchivePolicyDto>.Failure(ResultType.NotFound, AgendaErrors.KurinNotFound, "Kurin was not found.");
        }

        var changes = AuditChangeRules.Of(
            ("autoArchiveAfterDays", kurin.TaskAutoArchiveAfterDays, request.AutoArchiveAfterDays),
            ("purgeAfterDays", kurin.TaskArchivePurgeAfterDays, request.PurgeAfterDays));

        kurin.TaskAutoArchiveAfterDays = request.AutoArchiveAfterDays;
        kurin.TaskArchivePurgeAfterDays = request.PurgeAfterDays;
        if (changes.Count > 0)
        {
            await _audit.RecordAsync(
                new AuditRecord(AuditArea.Settings, AuditActionNames.Settings.ArchivePolicyChanged, kurin.KurinKey, Label, Changes: changes, KurinKey: kurin.KurinKey),
                cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        return new ServiceResult<AgendaArchivePolicyDto>(ResultType.Success,
            new AgendaArchivePolicyDto { AutoArchiveAfterDays = kurin.TaskAutoArchiveAfterDays, PurgeAfterDays = kurin.TaskArchivePurgeAfterDays });
    }
}
