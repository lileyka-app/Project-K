using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.MemberModule;
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
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, KurinErrors.ArchiveOutOfReach, "The archive is not yours to correct.");
        }

        var office = await _unitOfWork.Archive.FindOfficeAsync(request.KurinKey, request.HistoryKey, cancellationToken);
        if (office is null)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, KurinErrors.ArchiveRecordNotFound, "No such holding in this kurin.");
        }

        if (office.EndDate is null)
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, KurinErrors.ArchiveRecordCurrent, "A current holding is changed on the провід.");
        }

        if (reach.IsOwn(office.MemberKey))
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, KurinErrors.ArchiveOwnRecord, "Nobody corrects their own record.");
        }

        if (!reach.Reaches([office.GroupKey]))
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, KurinErrors.ArchiveOutOfReach, "The holding is outside your гуртки.");
        }

        if (!LeadershipOfficeRules.Grouping[office.Type].Contains(request.Role))
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, KurinErrors.ArchiveInvalidRole, "The role does not belong to this провід.");
        }

        var kurinNames = await _unitOfWork.Archive.GetRoleNamesAsync(request.KurinKey, cancellationToken);
        var changes = AuditChangeRules.Of(
            ("role", KurinRoleNames.Label(office.Role, kurinNames), KurinRoleNames.Label(request.Role, kurinNames)),
            ("startDate", office.StartDate, request.StartDate),
            ("endDate", office.EndDate, request.EndDate));
        if (changes.Count == 0)
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        await _unitOfWork.Archive.CorrectOfficeAsync(office.HistoryKey, request.Role, request.StartDate, request.EndDate, cancellationToken);

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Office,
                AuditActionNames.Office.Corrected,
                office.HistoryKey,
                office.GroupName is { } correctedGroup ? $"{KurinRoleNames.Label(request.Role, kurinNames)} · {correctedGroup}" : KurinRoleNames.Label(request.Role, kurinNames),
                office.MemberKey,
                changes,
                KurinKey: request.KurinKey),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
