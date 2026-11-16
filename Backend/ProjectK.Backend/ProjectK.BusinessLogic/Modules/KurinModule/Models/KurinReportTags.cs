using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

/// <summary>
/// The tags the звіт куреня prints: whether the reader sees anyone's at all, and the names on one
/// person — empty for a person whose tags are hidden from the reader.
/// </summary>
public sealed record KurinReportTags(bool Shown, Func<KurinReportSourceMember, IReadOnlyList<string>> Of);
