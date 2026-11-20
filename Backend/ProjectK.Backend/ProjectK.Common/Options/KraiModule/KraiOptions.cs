using ProjectK.Common.Entities.KraiModule;

namespace ProjectK.Common.Options.KraiModule;

/// <summary>Which край a new kurin joins when nobody names one. Self-host sets it with <c>PROJECTK_DEFAULT_KRAI</c>.</summary>
public sealed class KraiOptions
{
    public const string SectionName = "Krai";

    public string Default { get; init; } = Krai.Ukraine;
}
