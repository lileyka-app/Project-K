using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Records.InfrastructureModule;

namespace ProjectK.Infrastructure.Services.Feedback;

/// <summary>
/// What runs when no tracker token is configured: the report is written to the log in the same
/// shape it would have had as an issue, so a self-host owner reading the log gets the whole thing.
/// </summary>
public sealed class LogProblemReporter : IProblemReporter
{
    private readonly ILogger<LogProblemReporter> _logger;
    private readonly string? _apiVersion;

    public LogProblemReporter(ILogger<LogProblemReporter> logger, IConfiguration configuration)
    {
        _logger = logger;
        _apiVersion = configuration["ReleaseInfo:Version"];
    }

    public Task<ProblemReportReceipt> ReportAsync(ProblemReport report, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Problem report (no tracker configured): {Title}\n{Body}",
            report.Title,
            GitHubIssueBody.Compose(report, _apiVersion));

        return Task.FromResult(new ProblemReportReceipt(null, null));
    }
}
