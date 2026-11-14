namespace ProjectK.Common.Records.KurinModule;

/// <summary>A впорядник's assignment to one of the kurin's гуртки, by their account.</summary>
public sealed record KurinReportSourceMentor(Guid MentorUserKey, Guid GroupKey, DateTime? RevokedAtUtc);
