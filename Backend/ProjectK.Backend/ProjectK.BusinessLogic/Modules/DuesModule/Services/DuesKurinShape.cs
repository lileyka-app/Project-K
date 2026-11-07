using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Tags;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// What the dues module needs to know about a kurin's shape: whether it keeps its people in гуртки
/// or each in the kurin's own box, and which tags they carry — the tags set a senior kurin's rates.
/// </summary>
public interface IDuesKurinShape
{
    /// <summary>A senior kurin (УСП, УПС) has no гуртки: every person has an account in the kurin's box.</summary>
    Task<bool> IsSeniorAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>Whether the kurin uses tags now. With tags off, everyone pays the rate for everyone.</summary>
    Task<bool> TagsOnAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The kurin's tags on each of its people now, by member. Someone without tags is absent; with the
    /// kurin's tags off, everyone is.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> TagsByMemberAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class DuesKurinShape : IDuesKurinShape
{
    private readonly IUnitOfWork _unitOfWork;

    public DuesKurinShape(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> IsSeniorAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken);
        return kurin is not null && KurinBranchSet.IsSenior(kurin.Branch);
    }

    public async Task<bool> TagsOnAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken);
        if (kurin is null)
        {
            return false;
        }

        var settings = await _unitOfWork.KurinTags.GetSettingsAsync(kurinKey, cancellationToken);
        return settings?.Enabled ?? KurinTagRules.EnabledByDefault(kurin.Branch);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> TagsByMemberAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        if (!await TagsOnAsync(kurinKey, cancellationToken))
        {
            return new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        }

        return (await _unitOfWork.KurinTags.GetAssignmentsAsync(kurinKey, cancellationToken))
            .GroupBy(a => a.MemberKey)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<Guid>)g.Select(a => a.KurinTagKey).ToList());
    }
}
