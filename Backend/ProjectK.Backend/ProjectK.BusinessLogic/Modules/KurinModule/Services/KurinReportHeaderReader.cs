using Microsoft.Extensions.Configuration;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// The звіт куреня's header: when it was made, by whom, and the release that made it.
/// </summary>
public sealed class KurinReportHeaderReader
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IConfiguration _configuration;

    public KurinReportHeaderReader(ICurrentUserContext currentUser, IConfiguration configuration)
    {
        _currentUser = currentUser;
        _configuration = configuration;
    }

    /// <summary>The header for a report made now by the current user, named from <paramref name="usersByKey"/>.</summary>
    public KurinReportHeader Read(IReadOnlyDictionary<Guid, AppUser> usersByKey)
        => new(
            DateTime.UtcNow,
            ResolveCurrentUserName(usersByKey),
            ResolveCurrentUserEmail(usersByKey),
            ResolveReleaseInfo("Version"),
            // Not through ResolveReleaseInfo: its "unknown" fallback would print as a code name.
            ReleaseDisplayRules.CodeName(_configuration["ReleaseInfo:Codename"] ?? _configuration["ReleaseInfo:CodeName"]));

    private string ResolveCurrentUserName(IReadOnlyDictionary<Guid, AppUser> usersByKey)
    {
        if (_currentUser.UserId is Guid userKey && usersByKey.TryGetValue(userKey, out var user))
        {
            var fullName = PersonNames.FirstLast(user.FirstName, user.LastName);
            return string.IsNullOrWhiteSpace(fullName) ? user.Email ?? user.UserName ?? user.Id.ToString() : fullName;
        }

        return "Unknown user";
    }

    private string? ResolveCurrentUserEmail(IReadOnlyDictionary<Guid, AppUser> usersByKey)
    {
        return _currentUser.UserId is Guid userKey && usersByKey.TryGetValue(userKey, out var user)
            ? user.Email
            : null;
    }

    private string ResolveReleaseInfo(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[$"ReleaseInfo:{key}"];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "unknown";
    }
}
