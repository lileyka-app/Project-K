using ProjectK.Common.Dtos.DuesModule;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>The kurin's tags and the ones on each person, as far as the viewer may see them.</summary>
public sealed class DuesTagLens
{
    private readonly IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> _byMember;
    private readonly Func<Guid?, Guid?, bool> _shows;

    public DuesTagLens(IReadOnlyList<DuesTagDto> tags, IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> byMember, Func<Guid?, Guid?, bool> shows)
    {
        All = tags;
        _byMember = byMember;
        _shows = shows;
    }

    /// <summary>The kurin's tags, for naming and filtering by them.</summary>
    public IReadOnlyList<DuesTagDto> All { get; }

    /// <summary>Whether the viewer may see the tags on a person.</summary>
    public bool Shows(Guid? accountKey, Guid? groupKey) => _shows(accountKey, groupKey);

    public IReadOnlyList<Guid> Of(Guid memberKey, Guid? accountKey, Guid? groupKey) =>
        _shows(accountKey, groupKey) && _byMember.TryGetValue(memberKey, out var keys) ? [.. keys] : [];
}
