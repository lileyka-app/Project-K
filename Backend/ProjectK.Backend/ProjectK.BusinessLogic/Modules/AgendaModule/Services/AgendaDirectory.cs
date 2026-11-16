using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Records.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <inheritdoc />
public sealed class AgendaDirectory : IAgendaDirectory
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IOfficeDirectory _offices;
    private readonly IKurinTagDirectory _tags;

    public AgendaDirectory(IUnitOfWork uow, IMemberDirectory members, IOfficeDirectory offices, IKurinTagDirectory tags)
    {
        _uow = uow;
        _members = members;
        _offices = offices;
        _tags = tags;
    }

    public Task<AgendaRoster> GetRosterAsync(
        Guid kurinKey,
        IReadOnlyCollection<AgendaItem> items,
        CancellationToken cancellationToken = default)
        => AgendaRosterRules.LoadAsync(_members, _offices, _tags, kurinKey, items, cancellationToken);
}
