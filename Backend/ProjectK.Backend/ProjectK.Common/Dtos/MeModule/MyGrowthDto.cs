namespace ProjectK.Common.Dtos.MeModule;

/// <summary>
/// The youth programme as it stands for the person: the проба they are on and their вмілості. Only
/// for someone whose own branch is УПЮ; for anyone else <see cref="HasYouthProgram"/> is false and
/// the rest is empty, so the dashboard shows nothing of it.
/// </summary>
public sealed record MyGrowthDto
{
    public required Guid MemberKey { get; init; }
    public required bool HasYouthProgram { get; init; }
    /// <summary>The проба in hand, or the next one to start; null once every проба is verified.</summary>
    public required MyProbeDto? Probe { get; init; }
    public required MyBadgesDto Badges { get; init; }
}
