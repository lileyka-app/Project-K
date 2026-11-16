namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>Where a person's вкладка is kept: a гурток's box, or with no <see cref="GroupKey"/> the kurin's own.</summary>
public sealed record DuesBox(Guid KurinKey, Guid? GroupKey);
