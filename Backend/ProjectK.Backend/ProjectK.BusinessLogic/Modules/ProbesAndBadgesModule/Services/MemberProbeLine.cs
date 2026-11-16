using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Records.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

public sealed class MemberProbeLine : IMemberProbeLine, IProbeEditionHolds
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbesCatalogReader _catalog;
    private readonly IMemberDirectory _members;
    private readonly IGroupProbeEditions _groupEditions;

    public MemberProbeLine(
        IUnitOfWork unitOfWork,
        IProbesCatalogReader catalog,
        IMemberDirectory members,
        IGroupProbeEditions groupEditions)
    {
        _unitOfWork = unitOfWork;
        _catalog = catalog;
        _members = members;
        _groupEditions = groupEditions;
    }

    public async Task<IReadOnlyList<ProbeSummaryDto>> ForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
    {
        return (await ReadAsync(memberKey, cancellationToken))?.Line ?? [];
    }

    public async Task<ProbeStart> CanStartAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default)
    {
        if (await ReadAsync(memberKey, cancellationToken) is not { } read)
        {
            return ProbeStart.NotInLine;
        }

        var place = read.Line.ToList().FindIndex(probe => probe.Id == probeId);
        if (place < 0)
        {
            return ProbeStart.NotInLine;
        }

        if (place == 0)
        {
            return ProbeStart.Allowed;
        }

        return read.Progress.TryGetValue(read.Line[place - 1].Id, out var previous) && ProbeLineRules.IsClosed(previous)
            ? ProbeStart.Allowed
            : ProbeStart.WaitsForPrevious;
    }

    public async Task<IReadOnlyDictionary<Guid, ProbeEditionHold>> ForMembersAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default)
    {
        if (memberKeys.Count == 0)
        {
            return new Dictionary<Guid, ProbeEditionHold>();
        }

        var catalog = _catalog.GetProbes();
        var rows = await _unitOfWork.ProbeProgresses.GetByMemberKeysAsync(memberKeys, cancellationToken);

        var standings = new Dictionary<Guid, ProbeEditionHold>();
        foreach (var person in rows.GroupBy(row => row.MemberKey))
        {
            var statuses = Statuses(person);
            if (ProbeLineRules.HeldBy(catalog, statuses) is { } held)
            {
                standings[person.Key] = new ProbeEditionHold(person.Key, held.Edition, held.Title, IsHeld: true);
                continue;
            }

            var underWay = catalog
                .Where(probe => statuses.TryGetValue(probe.Id, out var status) && status == ProbeProgressStatus.InProgress)
                .OrderByDescending(probe => probe.Order)
                .FirstOrDefault();
            if (underWay is not null)
            {
                standings[person.Key] = new ProbeEditionHold(person.Key, underWay.Edition, underWay.Title, IsHeld: false);
            }
        }

        return standings;
    }

    private async Task<(IReadOnlyList<ProbeSummaryDto> Line, IReadOnlyDictionary<string, ProbeProgressStatus> Progress)?> ReadAsync(
        Guid memberKey,
        CancellationToken cancellationToken)
    {
        if (await _members.FindKurinKeyAsync(memberKey, cancellationToken) is not { } kurinKey)
        {
            return null;
        }

        var progress = Statuses(await _unitOfWork.ProbeProgresses.GetByMemberKeyAsync(memberKey, cancellationToken));
        var groupEdition = await _groupEditions.ForMemberAsync(memberKey, kurinKey, cancellationToken);

        return (ProbeLineRules.Of(_catalog.GetProbes(), progress, groupEdition), progress);
    }

    // One row per probe is the rule; should a duplicate slip in, the furthest status counts.
    private static Dictionary<string, ProbeProgressStatus> Statuses(IEnumerable<ProbeProgress> rows) =>
        rows.GroupBy(row => row.ProbeId, StringComparer.Ordinal)
            .ToDictionary(probe => probe.Key, probe => probe.Max(row => row.Status), StringComparer.Ordinal);
}
