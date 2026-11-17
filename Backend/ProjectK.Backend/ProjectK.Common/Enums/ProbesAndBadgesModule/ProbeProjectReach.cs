namespace ProjectK.Common.Enums.ProbesAndBadgesModule;

/// <summary>Whom a project is useful to, as the template lists it.</summary>
[Flags]
public enum ProbeProjectReach
{
    None = 0,
    GroupOrKurin = 1,
    Stanytsia = 2,
    UkrainianCommunity = 4,
    WiderSociety = 8
}
