namespace ProjectK.Common.Records.KurinModule;

public sealed record KurinReportKurin(
    Guid KurinKey,
    int? Number,
    string? Stanytsia,
    string? RegionOrCountry,
    string? NamedAfter,
    string? Description,
    string? SeniorBranches = null)
{
    /// <summary>A УСП or УПС kurin: named by its own name and its гілки, with no УПЮ number plate.</summary>
    public bool IsSenior => SeniorBranches is not null;
}
