namespace ProjectK.Common.Models.Enums;

/// <summary>What a kurin asks of the kurin a person stands in. Stored as a number — the values are frozen.</summary>
public enum MembershipRequestKind
{
    /// <summary>«Перевести»: the membership in the person's kurin closes, and one in the asking kurin opens.</summary>
    Transfer = 0,

    /// <summary>«Прийняти ще й до нас»: the person keeps their kurin and stands in the asking one too.</summary>
    AlsoJoin = 1,
}
