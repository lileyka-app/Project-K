using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Options.InfrastructureModule;
using ProjectK.Common.Records.InfrastructureModule;

namespace ProjectK.Infrastructure.Services.Feedback;

/// <summary>
/// Opens a GitHub issue per report with a token that lives only on the server. The person never
/// touches GitHub and never sees the token; the issue carries no name or e-mail, only the opaque
/// account key so the maintainer can look the reporter up.
/// </summary>
public sealed class GitHubProblemReporter : IProblemReporter
{
    private readonly HttpClient _http;
    private readonly GitHubFeedbackOptions _options;
    private readonly string? _apiVersion;
    private readonly ILogger<GitHubProblemReporter> _logger;

    public GitHubProblemReporter(
        HttpClient http,
        IOptions<FeedbackOptions> options,
        IConfiguration configuration,
        ILogger<GitHubProblemReporter> logger)
    {
        _http = http;
        _options = options.Value.GitHub;
        _apiVersion = configuration["ReleaseInfo:Version"];
        _logger = logger;

        _http.BaseAddress ??= new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Lileyka", _apiVersion ?? "dev"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
    }

    public async Task<ProblemReportReceipt> ReportAsync(ProblemReport report, CancellationToken cancellationToken)
    {
        var labels = _options.Labels
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var payload = new NewIssue(report.Title, GitHubIssueBodyRules.Compose(report, _apiVersion), labels);
        using var response = await _http.PostAsJsonAsync($"repos/{_options.Repository}/issues", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("GitHub refused the issue: {Status}. {Detail}", (int)response.StatusCode, Truncate(detail));
            throw new InvalidOperationException($"GitHub answered {(int)response.StatusCode}.");
        }

        var issue = await response.Content.ReadFromJsonAsync<CreatedIssue>(cancellationToken);
        _logger.LogInformation("Problem report became issue #{Number}.", issue?.Number);
        return new ProblemReportReceipt(issue?.HtmlUrl, issue?.Number);
    }

    private static string Truncate(string text) => text.Length <= 500 ? text : text[..500] + "…";

    private sealed record NewIssue(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("labels")] string[] Labels);

    private sealed record CreatedIssue(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("html_url")] string HtmlUrl);
}
