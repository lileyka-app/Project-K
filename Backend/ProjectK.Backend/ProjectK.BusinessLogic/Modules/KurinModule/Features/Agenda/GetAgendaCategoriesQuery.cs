using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

/// <summary>Event groups of a kurin. Archived ones are included only for the management page.</summary>
public sealed record GetAgendaCategoriesQuery(Guid KurinKey, bool IncludeArchived)
    : IRequest<ServiceResult<IEnumerable<AgendaCategoryDto>>>;

public sealed class GetAgendaCategoriesQueryHandler
    : IRequestHandler<GetAgendaCategoriesQuery, ServiceResult<IEnumerable<AgendaCategoryDto>>>
{
    private readonly IUnitOfWork _uow;

    public GetAgendaCategoriesQueryHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ServiceResult<IEnumerable<AgendaCategoryDto>>> Handle(GetAgendaCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _uow.AgendaCategories.GetForKurinAsync(request.KurinKey, request.IncludeArchived, cancellationToken);
        var responses = categories.Select(category => category.ToDto()).ToList();
        return new ServiceResult<IEnumerable<AgendaCategoryDto>>(ResultType.Success, responses);
    }
}
