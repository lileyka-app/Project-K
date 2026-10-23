using ProjectK.Common.Models.Members;
using MemberEntity = ProjectK.Common.Entities.KurinModule.Member;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Upsert;

/// <summary>
/// Write protection for the groups of <see cref="MemberDataFields"/> the writer may not see. Their
/// form showed «Приховано» and sent blanks back, so whatever came for those groups counts as not
/// sent and the stored values stay.
/// </summary>
public static class HiddenDataOnWrite
{
    /// <summary>Puts the stored values of every hidden group back into <paramref name="request"/>.</summary>
    public static void KeepStored(IReadOnlySet<MemberDataGroup> hidden, UpsertMemberProfileCommand request, MemberEntity existing)
    {
        if (hidden.Contains(MemberDataGroup.Contacts))
        {
            request.Email = existing.Email;
            request.PhoneNumber = existing.PhoneNumber;
        }

        if (hidden.Contains(MemberDataGroup.BirthDate))
        {
            request.DateOfBirth = existing.DateOfBirth;
        }

        if (hidden.Contains(MemberDataGroup.Address))
        {
            request.Address = existing.Address;
            request.School = existing.School;
        }

        if (hidden.Contains(MemberDataGroup.Parents))
        {
            request.WithParentsOf(existing);
        }

        // A key left out keeps its value, so sending none keeps them all.
        if (hidden.Contains(MemberDataGroup.Extras))
        {
            request.CardExtras = null;
        }
    }
}
