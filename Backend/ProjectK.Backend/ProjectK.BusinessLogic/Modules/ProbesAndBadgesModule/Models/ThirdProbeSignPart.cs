namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;

/// <summary>What a leader confirms on the third probe, besides the probe's points.</summary>
public enum ThirdProbeSignPart
{
    /// <summary>The reflection with the Звʼязковий took place. (The camp has no confirmation: the край counts it, not Lilejka.)</summary>
    Reflection,

    /// <summary>«Дозвіл»: the project may go ahead.</summary>
    ProjectApproval,

    /// <summary>«Успішно виконав»: the project is done, with the coordinator's comment.</summary>
    ProjectCompletion
}
