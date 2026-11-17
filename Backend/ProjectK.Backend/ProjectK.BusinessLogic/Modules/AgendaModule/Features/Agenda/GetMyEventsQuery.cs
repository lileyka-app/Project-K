using FluentValidation;
using MediatR;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;

/// <summary>What is ahead for the person in the next days, across every kurin they stand in.</summary>
public sealed record GetMyEventsQuery(int Days) : IRequest<ServiceResult<IReadOnlyList<MyEventDto>>>;

public sealed class GetMyEventsQueryValidator : AbstractValidator<GetMyEventsQuery>
{
    public GetMyEventsQueryValidator()
    {
        RuleFor(q => q.Days).InclusiveBetween(1, 90);
    }
}

public sealed class GetMyEventsQueryHandler : IRequestHandler<GetMyEventsQuery, ServiceResult<IReadOnlyList<MyEventDto>>>
{
    private readonly IAgendaViewerDirectory _scopes;
    private readonly IAgendaDirectory _agenda;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileDirectory _profiles;

    public GetMyEventsQueryHandler(IAgendaViewerDirectory scopes, IAgendaDirectory agenda, ICurrentUserContext currentUser, TimeProvider time, IKraiProfileDirectory profiles)
    {
        _scopes = scopes;
        _agenda = agenda;
        _currentUser = currentUser;
        _time = time;
        _profiles = profiles;
    }

    public async Task<ServiceResult<IReadOnlyList<MyEventDto>>> Handle(GetMyEventsQuery request, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var until = now.AddDays(request.Days);
        var rows = new List<MyEventDto>();

        foreach (var context in await _scopes.GetForCurrentAccountAsync(cancellationToken))
        {
            var kurin = MyKurinRefDto.Of(context.Membership, _currentUser.KurinKey);
            var zone = await _profiles.ForKurinAsync(context.Membership.KurinKey, cancellationToken);
            var items = await _agenda.GetInWindowForViewerAsync(
                context.Viewer, new AgendaWindow(now, until, zone, AgendaItemKind.Event, WithAnswers: true), cancellationToken);
            var categories = (await _agenda.GetCategoriesAsync(context.Membership.KurinKey, includeArchived: true, cancellationToken))
                .ToDictionary(c => c.AgendaCategoryKey);

            foreach (var item in items)
            {
                var category = item.AgendaCategoryKey is { } key ? categories.GetValueOrDefault(key) : null;

                foreach (var occurrence in item.Occurrences)
                {
                    rows.Add(ToDto(item, kurin, category, occurrence));
                }
            }
        }

        return new ServiceResult<IReadOnlyList<MyEventDto>>(ResultType.Success, rows.OrderBy(r => r.StartUtc).ThenBy(r => r.Title).ToList());
    }

    private static MyEventDto ToDto(AgendaViewerItem item, MyKurinRefDto kurin, AgendaCategorySummary? category, AgendaViewerOccurrence occurrence) => new()
    {
        AgendaItemKey = item.AgendaItemKey,
        Kurin = kurin,
        Title = item.Title,
        StartUtc = DateTime.SpecifyKind(occurrence.StartUtc, DateTimeKind.Utc),
        EndUtc = occurrence.EndUtc is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null,
        IsAllDay = item.IsAllDay,
        IsRecurring = item.IsRecurring,
        Location = item.Location,
        CategoryName = category?.Name,
        CategoryColorHex = category?.ColorHex,
        CategoryIcon = category?.Icon,
        RsvpRequired = category?.RsvpRequired ?? false,
        MyResponse = occurrence.Answer
    };
}
