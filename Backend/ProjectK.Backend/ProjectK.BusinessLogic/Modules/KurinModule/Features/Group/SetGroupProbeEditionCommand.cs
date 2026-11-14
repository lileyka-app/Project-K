using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

/// <summary>
/// Moves a гурток to another edition of the probe programme. Never refused for who is in it: a probe
/// under way stays in its edition, and whoever closed a probe past the first finishes theirs.
/// </summary>
/// <param name="Edition">Null or empty for the unnamed edition.</param>
public sealed record SetGroupProbeEditionCommand(Guid GroupKey, string? Edition) : IRequest<ServiceResult<Unit>>;

public sealed class SetGroupProbeEditionCommandHandler : IRequestHandler<SetGroupProbeEditionCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbesCatalog _catalog;
    private readonly IKurinAudit _audit;

    public SetGroupProbeEditionCommandHandler(IUnitOfWork unitOfWork, IProbesCatalog catalog, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _catalog = catalog;
        _audit = audit;
    }

    public async Task<ServiceResult<Unit>> Handle(SetGroupProbeEditionCommand request, CancellationToken cancellationToken)
    {
        var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        if (group is null)
        {
            return KurinErrors.GroupMissing<Unit>();
        }

        var edition = string.IsNullOrWhiteSpace(request.Edition) ? null : request.Edition.Trim();
        if (edition == group.ProbeEdition)
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(group.KurinKey, cancellationToken);
        var offered = OfferedProbeEditionRules.OfferedTo(_catalog, kurin?.KraiCode ?? string.Empty);
        if (!offered.Any(offer => offer.Id == edition))
        {
            return ServiceResult<Unit>.Failure(
                ResultType.BadRequest,
                KurinErrors.ProbeEditionNotOffered,
                "The catalogue offers this kurin no such edition of the probe programme.");
        }

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Settings,
                AuditActions.Settings.GroupProbeEditionChanged,
                group.GroupKey,
                group.Name,
                Changes: AuditChanges.Of(("probeEdition", TitleOf(offered, group.ProbeEdition), TitleOf(offered, edition))),
                KurinKey: group.KurinKey),
            cancellationToken);

        group.ProbeEdition = edition;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

    /// <summary>
    /// The journal keeps the name people saw at the time, not the catalogue id: a later rename in
    /// the catalogue must not rewrite history. Null stays null — the unnamed edition.
    /// </summary>
    private static string? TitleOf(IReadOnlyList<ProbeEditionDto> offered, string? edition) =>
        edition is null ? null : offered.FirstOrDefault(offer => offer.Id == edition)?.Title ?? edition;
}
