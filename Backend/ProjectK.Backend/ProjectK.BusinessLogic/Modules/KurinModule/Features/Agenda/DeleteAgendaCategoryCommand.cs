using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

/// <summary>
/// Removes an event group. Items that referenced it keep their dates and simply become uncategorised
/// (the FK is set to null), so deleting a group never deletes its history. Gated to Зв'язковий.
/// </summary>
public sealed record DeleteAgendaCategoryCommand(Guid AgendaCategoryKey, Guid KurinKey) : IRequest<ServiceResult<object>>;

public sealed class DeleteAgendaCategoryCommandHandler : IRequestHandler<DeleteAgendaCategoryCommand, ServiceResult<object>>
{
    private readonly IUnitOfWork _uow;

    public DeleteAgendaCategoryCommandHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ServiceResult<object>> Handle(DeleteAgendaCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _uow.AgendaCategories.GetByKeyAsync(request.AgendaCategoryKey, cancellationToken);
        if (category is null)
        {
            return ServiceResult<object>.Failure(ResultType.NotFound, "CATEGORY_NOT_FOUND", "Event group was not found.");
        }

        if (category.KurinKey != request.KurinKey)
        {
            return ServiceResult<object>.Failure(ResultType.Forbidden, "CATEGORY_OTHER_KURIN", "Event group belongs to a different kurin.");
        }

        // The item→category FK is NoAction, so detach any events from this group before removing it.
        await _uow.AgendaItems.ClearCategoryAsync(category.AgendaCategoryKey, cancellationToken);

        _uow.AgendaCategories.Delete(category, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return new ServiceResult<object>(ResultType.Success);
    }
}
