namespace ProjectK.Common.Dtos.InfrastructureModule;

/// <summary>Where the report went, when the tracker gave it an address.</summary>
public sealed record ProblemReportReceipt(string? IssueUrl, int? IssueNumber);
