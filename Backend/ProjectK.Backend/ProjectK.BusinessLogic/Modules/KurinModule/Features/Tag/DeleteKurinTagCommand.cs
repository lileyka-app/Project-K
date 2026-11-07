using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Tag;

/// <summary>
/// Deletes a tag and takes it off everyone who carried it. One journal entry says how many: an
/// entry per person would bury the journal for a single click. Allowed with tags off too, so a kurin
/// can clear what it no longer wants.
/// </summary>
public sealed record DeleteKurinTagCommand(Guid KurinKey, Guid KurinTagKey) : IRequest<ServiceResult<object>>;

public sealed class DeleteKurinTagCommandHandler : IRequestHandler<DeleteKurinTagCommand, ServiceResult<object>>
{
    private readonly IUnitOfWork _uow;
    private readonly IKurinAudit _audit;
    private readonly IDuesDirectory _dues;

    public DeleteKurinTagCommandHandler(IUnitOfWork uow, IKurinAudit audit, IDuesDirectory dues)
    {
        _uow = uow;
        _audit = audit;
        _dues = dues;
    }

    public async Task<ServiceResult<object>> Handle(DeleteKurinTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await _uow.KurinTags.GetWithMembersAsync(request.KurinTagKey, cancellationToken);
        if (tag is null || tag.KurinKey != request.KurinKey)
        {
            return ServiceResult<object>.Failure(ResultType.NotFound, KurinTagErrors.TagNotFound, "Tag was not found in this kurin.");
        }

        await _audit.RecordAsync(
            new AuditRecord(AuditArea.Tag, AuditActions.Tag.Deleted, tag.KurinTagKey, tag.Name,
                Changes: AuditChanges.Removed(("name", tag.Name), ("carriedBy", tag.Members.Count)),
                KurinKey: request.KurinKey),
            cancellationToken);

        // Events and tasks aimed at the tag name it by key alone; left behind, they would reach nobody.
        await _uow.AgendaItems.RemoveAssignmentsForTargetsAsync([tag.KurinTagKey], cancellationToken);
        _uow.KurinTags.Delete(tag, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        // A rate by this tag can no longer be seen or changed: nobody's debt may stay on it.
        await _dues.RepriceUnpaidAsync(request.KurinKey, cancellationToken);

        return new ServiceResult<object>(ResultType.Success);
    }
}
