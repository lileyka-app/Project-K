namespace ProjectK.Common.Rules.KraiModule;

public static class KraiCardFields
{
    public const string Required = "required";
    public const string Optional = "optional";
    public const string Hidden = "hidden";

    public const string Text = "text";
    public const string Date = "date";

    public const string MiddleName = "middleName";
    public const string PhoneNumber = "phoneNumber";
    public const string Address = "address";
    public const string School = "school";

    /// <summary>Mother's and father's name and phone, as one block of the card.</summary>
    public const string Parents = "parents";

    /// <summary>
    /// The card's fields a край may make optional or hide, each with how it works without a
    /// profile. Name, email and date of birth are not here: an account and the age rules need them.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Configurable = new Dictionary<string, string>
    {
        [MiddleName] = Required,
        [PhoneNumber] = Required,
        [Address] = Optional,
        [School] = Optional,
        [Parents] = Optional
    };

    /// <summary>How the card names its configurable fields to people.</summary>
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [MiddleName] = "По батькові",
        [PhoneNumber] = "Номер телефону",
        [Address] = "Адреса",
        [School] = "Школа",
        [Parents] = "Контакти батьків"
    };

    public static readonly IReadOnlyList<string> Modes = [Required, Optional, Hidden];
    public static readonly IReadOnlyList<string> Types = [Text, Date];
}
