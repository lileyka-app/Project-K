using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Contracts.InfrastructureModule;

/// <summary>
/// Hands a problem report to whoever tracks them. GitHub Issues in the cloud; the log when no
/// tracker is configured, so a self-host without a token still keeps what people wrote.
/// </summary>
public interface IProblemReporter
{
    Task<ProblemReportReceipt> ReportAsync(ProblemReport report, CancellationToken cancellationToken);
}
