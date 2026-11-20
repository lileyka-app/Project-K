using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// The tags the звіт куреня prints, by the same rule as on the screen: only those of people whose
/// tags the reader may see.
/// </summary>
public sealed class KurinReportTagReader
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKurinTagVisibility _tags;

    public KurinReportTagReader(IUnitOfWork unitOfWork, IKurinTagVisibility tags)
    {
        _unitOfWork = unitOfWork;
        _tags = tags;
    }

    /// <summary>The tags of the people in <paramref name="source"/>, read only when the reader sees some.</summary>
    public async Task<KurinReportTags> ReadAsync(Guid kurinKey, KurinReportSourceData source, CancellationToken cancellationToken)
    {
        var showsTagsOf = await _tags.ShowsTagsOfAsync(kurinKey, cancellationToken);
        var tagsShown = source.Members.Any(member => showsTagsOf(member.UserKey, KurinReportRosterRules.GroupOf(source, member.MemberKey)));
        var tagNamesByMember = tagsShown ? await TagNamesByMemberAsync(kurinKey, cancellationToken) : new Dictionary<Guid, IReadOnlyList<string>>();

        return new KurinReportTags(
            tagsShown,
            member => showsTagsOf(member.UserKey, KurinReportRosterRules.GroupOf(source, member.MemberKey))
                ? tagNamesByMember.GetValueOrDefault(member.MemberKey) ?? []
                : []);
    }

    // Every person's tags in the kurin by name, in one read of the tags and one of who carries them.
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> TagNamesByMemberAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var names = (await _unitOfWork.KurinTags.GetForKurinAsync(kurinKey, cancellationToken))
            .ToDictionary(tag => tag.KurinTagKey, tag => tag.Name);
        return (await _unitOfWork.KurinTags.GetAssignmentsAsync(kurinKey, cancellationToken))
            .Where(assignment => names.ContainsKey(assignment.KurinTagKey))
            .GroupBy(assignment => assignment.MemberKey)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(assignment => names[assignment.KurinTagKey])
                    .Order(StringComparer.CurrentCulture)
                    .ToArray());
    }
}
