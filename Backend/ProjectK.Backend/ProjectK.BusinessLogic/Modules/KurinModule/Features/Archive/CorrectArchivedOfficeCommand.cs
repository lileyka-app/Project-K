using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive;

/// <summary>
/// Corrects a former holding of an office: its dates, or its role within the same провід. A
/// current holding is not touched here — ending or changing it changes what someone may do, and
/// that is the провід page's job.
/// </summary>
public sealed record CorrectArchivedOfficeCommand(
    Guid KurinKey,
    Guid HistoryKey,
    LeadershipRole Role,
    DateOnly StartDate,
    DateOnly EndDate) : IRequest<ServiceResult<Unit>>;

public sealed class CorrectArchivedOfficeCommandValidator : AbstractValidator<CorrectArchivedOfficeCommand>
{
    public CorrectArchivedOfficeCommandValidator(TimeProvider time)
    {
        RuleFor(command => command.Role).IsInEnum();
        RuleFor(command => command.EndDate)
            .GreaterThanOrEqualTo(command => command.StartDate)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
    }
}

public sealed class CorrectArchivedOfficeCommandHandler : IRequestHandler<CorrectArchivedOfficeCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ArchiveReach _reach;
    private readonly IKurinAudit _audit;

    public CorrectArchivedOfficeCommandHandler(IUnitOfWork unitOfWork, ArchiveReach reach, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _reach = reach;
        _audit = audit;
    }

    public async Task<ServiceResult<Unit>> Handle(CorrectArchivedOfficeCommand request, CancellationToken cancellationToken)
    {
        if (await _reach.ResolveAsync(request.KurinKey, ResourceAction.Update, cancellationToken) is not { } reach)
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, ArchiveErrors.OutOfReach, "The archive is not yours to correct.");
        }

        var history = await _unitOfWork.Archive.FindOfficeAsync(request.HistoryKey, cancellationToken);
        var leadership = history?.Leadership;
        if (history is null || (leadership!.KurinKey ?? leadership.Group?.KurinKey) != request.KurinKey)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, ArchiveErrors.NotFound, "No such holding in this kurin.");
        }

        if (history.EndDate is null)
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, ArchiveErrors.StillCurrent, "A current holding is changed on the провід.");
        }

        if (reach.IsOwn(history.MemberKey))
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, ArchiveErrors.OwnRecord, "Nobody corrects their own record.");
        }

        if (!reach.Reaches([leadership.GroupKey]))
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, ArchiveErrors.OutOfReach, "The holding is outside your гуртки.");
        }

        if (!LeadershipOffices.Grouping[leadership.Type].Contains(request.Role))
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, ArchiveErrors.InvalidRole, "The role does not belong to this провід.");
        }

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        var kurinNames = KurinRoleNames.Read(kurin?.RoleNames);
        var changes = AuditChanges.Of(
            ("role", ArchiveErrors.RoleLabel(history.Role, kurinNames), ArchiveErrors.RoleLabel(request.Role, kurinNames)),
            ("startDate", history.StartDate, request.StartDate),
            ("endDate", history.EndDate, request.EndDate));
        if (changes.Count == 0)
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        history.Role = request.Role;
        history.StartDate = request.StartDate;
        history.EndDate = request.EndDate;

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Office,
                AuditActions.Office.Corrected,
                history.LeadershipHistoryKey,
                leadership.Group?.Name is { } correctedGroup ? $"{ArchiveErrors.RoleLabel(request.Role, kurinNames)} · {correctedGroup}" : ArchiveErrors.RoleLabel(request.Role, kurinNames),
                history.MemberKey,
                changes,
                KurinKey: request.KurinKey),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
