using ProjectK.Common.Models.Members;

namespace ProjectK.Common.Interfaces.Modules.AuthModule;

/// <summary>
/// The current request's <see cref="MemberFieldMask"/>: what the caller may see of other people's
/// data under the policy of the kurin their token is scoped to. Worked out once per request.
/// </summary>
public interface IMemberFieldAccess
{
    /// <summary>The caller's mask.</summary>
    Task<MemberFieldMask> ForViewerAsync(CancellationToken cancellationToken = default);
}
