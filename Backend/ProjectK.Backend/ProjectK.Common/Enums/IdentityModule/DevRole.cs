namespace ProjectK.Common.Enums.IdentityModule;

/// <summary>The seats the dev role switcher can put an administrator into.</summary>
public enum DevRole
{
    Zvyazkovyi,
    Vykhovnyk,
    Kurinnyi,
    Skarbnyk,
    /// <summary>A member of the kurin with no kurin-wide or КВ office and no гурток to mentor; a гуртковий office is fine.</summary>
    Member
}
