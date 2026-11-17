using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Records.NotificationsModule;

namespace ProjectK.Common.Contracts.NotificationsModule;

/// <summary>
/// Hands a problem report to whoever tracks them. GitHub Issues in the cloud; the log when no
/// tracker is configured, so a self-host without a token still keeps what people wrote.
/// </summary>
public interface IProblemReporter
{
    Task<ProblemReportReceipt> ReportAsync(ProblemReport report, CancellationToken cancellationToken);
}
