namespace ProjectK.Common.Models.Enums;

/// <summary>The five areas of the third probe («Скобиний Лет», 2023): one project from each.</summary>
public enum ProbeProjectArea
{
    /// <summary>А. Пластова присяга, ідея та організація.</summary>
    OathAndIdea,

    /// <summary>Б. Громадська діяльність.</summary>
    Community,

    /// <summary>В. Пластові заняття.</summary>
    PlastActivities,

    /// <summary>Г. Життя в природі.</summary>
    Nature,

    /// <summary>Ґ. Провідництво.</summary>
    Leadership
}

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

/// <summary>The camp a third-prober takes part in.</summary>
public enum ThirdProbeCampKind
{
    /// <summary>Курс виховників табору.</summary>
    Kvt,

    /// <summary>Another camp the край counts for the third probe.</summary>
    Alternative
}
