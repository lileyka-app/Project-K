using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

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
        return kurin is not null && KurinBranchSetRules.IsSenior(kurin.Branch);
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
