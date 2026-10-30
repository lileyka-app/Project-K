using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>Whether a probe nobody has started for a person yet may be started now.</summary>
public enum ProbeStart
{
    Allowed,

    /// <summary>The probe is not one this person walks: another edition, or not in the catalogue.</summary>
    NotInLine,

    /// <summary>The probe before it in the line is not closed yet.</summary>
    WaitsForPrevious
}

/// <summary>
/// A person's <see cref="ProbeLine"/>, read from their progress, their гурток's edition and the
/// catalogue. The one place clients, the dashboard and the guards on signing take it from.
/// </summary>
public interface IMemberProbeLine
{
    /// <summary>Empty when the member does not exist.</summary>
    Task<IReadOnlyList<ProbeSummaryResponse>> ForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>Asked only for a probe the person has not started: one already under way may go on.</summary>
    Task<ProbeStart> CanStartAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);
}

public sealed class MemberProbeLine : IMemberProbeLine, IProbeEditionHolds
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbesCatalogService _catalog;
    private readonly IMemberDirectory _members;
    private readonly IGroupProbeEditions _groupEditions;

    public MemberProbeLine(
        IUnitOfWork unitOfWork,
        IProbesCatalogService catalog,
        IMemberDirectory members,
        IGroupProbeEditions groupEditions)
    {
        _unitOfWork = unitOfWork;
        _catalog = catalog;
        _members = members;
        _groupEditions = groupEditions;
    }

    public async Task<IReadOnlyList<ProbeSummaryResponse>> ForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
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

        return read.Progress.TryGetValue(read.Line[place - 1].Id, out var previous) && ProbeLine.IsClosed(previous)
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
            if (ProbeLine.HeldBy(catalog, statuses) is { } held)
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

    private async Task<(IReadOnlyList<ProbeSummaryResponse> Line, IReadOnlyDictionary<string, ProbeProgressStatus> Progress)?> ReadAsync(
        Guid memberKey,
        CancellationToken cancellationToken)
    {
        if (await _members.FindKurinKeyAsync(memberKey, cancellationToken) is not { } kurinKey)
        {
            return null;
        }

        var progress = Statuses(await _unitOfWork.ProbeProgresses.GetByMemberKeyAsync(memberKey, cancellationToken));
        var groupEdition = await _groupEditions.ForMemberAsync(memberKey, kurinKey, cancellationToken);

        return (ProbeLine.Of(_catalog.GetProbes(), progress, groupEdition), progress);
    }

    // One row per probe is the rule; should a duplicate slip in, the furthest status counts.
    private static Dictionary<string, ProbeProgressStatus> Statuses(IEnumerable<ProbeProgress> rows) =>
        rows.GroupBy(row => row.ProbeId, StringComparer.Ordinal)
            .ToDictionary(probe => probe.Key, probe => probe.Max(row => row.Status), StringComparer.Ordinal);
}

/// <summary>What signing or moving a probe answers when <see cref="IMemberProbeLine.CanStartAsync"/> says no.</summary>
public static class ProbeStartRefusal
{
    public static ServiceResult<T>? Of<T>(ProbeStart start) => start switch
    {
        ProbeStart.NotInLine => ServiceResult<T>.Failure(
            ResultType.Conflict, "ProbeNotInLine", "This probe is not one the member walks: it belongs to another edition of the programme."),
        ProbeStart.WaitsForPrevious => ServiceResult<T>.Failure(
            ResultType.Conflict, "ProbeWaitsForPrevious", "This probe opens once the one before it is closed."),
        _ => null
    };
}
