using ProjectK.Common.Models.Members;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

/// <summary>What a <see cref="MemberResponse"/> carries beyond the mask's own groups.</summary>
public static class MemberResponseMasking
{
    /// <summary>
    /// Masks the person's data for the viewer and clears their public code unless it is their own:
    /// leadership that knew it could sign them into another kurin without ever asking them.
    /// </summary>
    public static MemberResponse MaskFor(this MemberResponse response, MemberFieldMask mask, Guid? groupKey)
    {
        mask.Apply(response, response.UserKey, groupKey);
        if (!mask.IsSelf(response.UserKey))
        {
            response.PublicId = null;
        }

        return response;
    }
}
