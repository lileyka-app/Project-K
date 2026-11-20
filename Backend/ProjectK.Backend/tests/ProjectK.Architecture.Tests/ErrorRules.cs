using System.Text.RegularExpressions;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// Error codes come from the module's <c>&lt;M&gt;Errors</c> catalogue (POL-07). Written as literals in
/// the handlers, "kurin not found" managed to become four different strings, and the frontend can
/// only word the ones it knows.
/// </summary>
public class ErrorRules
{
    private const string Errors = "CONTRIBUTING.md › Бекенд › «Відповіді та помилки»";

    /// <summary><c>Failure(ResultType.X, "Code", …)</c>, the code written in place.</summary>
    private static readonly Regex LiteralCode = new(
        @"\bFailure\s*\(\s*[^,()""]+,\s*\$?""(?<code>[^""]*)""", RegexOptions.CultureInvariant);

    /// <summary><c>ErrorCode = "Code"</c> on a hand-built result: the same literal by the side door.</summary>
    private static readonly Regex LiteralErrorCode = new(
        @"\bErrorCode\s*=\s*\$?""(?<code>[^""]*)""", RegexOptions.CultureInvariant);

    /// <summary><c>new ServiceResult&lt;T&gt;(ResultType.NotFound)</c> with no code at all.</summary>
    private static readonly Regex CodelessFailure = new(
        @"new\s+ServiceResult<[^;]*?>\(\s*ResultType\.(?<type>\w+)\s*\)(?!\s*\{)", RegexOptions.CultureInvariant);

    private static readonly string[] SuccessTypes = ["Success", "Created"];

    private static IEnumerable<string> HandlerFiles() =>
        SourceTree.Files("ProjectK.BusinessLogic")
            .Where(file => !Path.GetFileName(file).EndsWith("Errors.cs", StringComparison.Ordinal));

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    [Fact]
    public void BusinessLogic_ShouldTakeErrorCodesFromTheModuleCatalogue()
    {
        var offenders = HandlerFiles()
            .SelectMany(file =>
            {
                var text = SourceTree.Read(file);
                return LiteralCode.Matches(text).Concat(LiteralErrorCode.Matches(text))
                    .Select(match => $"{file}:{LineOf(text, match.Index)} \"{match.Groups["code"].Value}\" — use a constant from Modules/<M>/<M>Errors.cs");
            });

        Baseline.None(offenders, $"Error codes written as literals outside the <M>Errors catalogue ({Errors}):");
    }

    /// <summary>
    /// The e2e fixture endpoint answers for the test harness alone; its codes are no module's and no
    /// screen words them (D-02 in the extraction backlog).
    /// </summary>
    private static readonly string[] ApiLiteralCodes =
    [
        "ProjectK.API/Controllers/TestModule/E2ETestController.cs",
    ];

    /// <summary>
    /// A controller that refuses on its own (a missing file, an unknown role) takes the code from the
    /// module's catalogue too: the client words it by the same string the handler would send.
    /// </summary>
    [Fact]
    public void Api_ShouldTakeErrorCodesFromTheModuleCatalogue()
    {
        var offenders = SourceTree.Files("ProjectK.API")
            .Where(file => LiteralCode.IsMatch(SourceTree.Read(file)) || LiteralErrorCode.IsMatch(SourceTree.Read(file)));

        Baseline.Hold(
            offenders,
            ApiLiteralCodes,
            $"API files that pass an error code as a literal; take it from Modules/<M>/<M>Errors.cs ({Errors}):");
    }

    [Fact]
    public void BusinessLogic_ShouldGiveEveryFailureACode()
    {
        var offenders = HandlerFiles()
            .SelectMany(file =>
            {
                var text = SourceTree.Read(file);
                return CodelessFailure.Matches(text)
                    .Where(match => !SuccessTypes.Contains(match.Groups["type"].Value, StringComparer.Ordinal))
                    .Select(match => $"{file}:{LineOf(text, match.Index)} ResultType.{match.Groups["type"].Value} — return ServiceResult<T>.Failure(type, <M>Errors.Code, message)");
            });

        Baseline.None(offenders, $"Failures without an error code ({Errors}):");
    }
}
