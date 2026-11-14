using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// Who may withdraw a planning session, answered once for the responses that carry the flag.
/// <para>
/// The endpoint is guarded by <c>ResourceAuthorize</c>, which reaches the same conclusion per
/// request. This exists so the answer can travel <i>with</i> the session: the client needs to know
/// whether to offer the control at all, and the alternative — the frontend re-deriving "author or
/// whole-kurin management" from role names — is the drift that left the Курінний staring at a page
/// he was allowed to edit.
/// </para>
/// </summary>
public static class PlanningSessionAccess
{
    public static bool CanDelete(ICurrentUserContext currentUser, Guid? createdByUserKey)
    {
        if (currentUser.UserId is not { } userId)
        {
            return false;
        }

        if (createdByUserKey is { } author && author == userId)
        {
            return true;
        }

        return RolePermissionMap.WidestScope(
            RolePermissionMap.Resolve(currentUser.Roles),
            ResourceType.PlanningSession,
            ResourceAction.Delete) == AccessScope.KurinWide;
    }
}
