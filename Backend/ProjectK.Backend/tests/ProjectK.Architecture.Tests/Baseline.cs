namespace ProjectK.Architecture.Tests;

/// <summary>
/// Known debt, named one by one. A rule that reads names, folders or source text gives the same
/// answer in Debug and Release, so its baseline is held exactly: a new offender fails the rule, and so
/// does an entry that has since been fixed, until it is struck from the list. The list only gets shorter.
/// </summary>
public static class Baseline
{
    public static void Hold(IEnumerable<string> offenders, IReadOnlyCollection<string> baseline, string rule)
    {
        var found = offenders.Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();
        var added = found.Except(baseline, StringComparer.Ordinal).ToList();
        var fixedEntries = baseline.Except(found, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();

        if (added.Count > 0)
        {
            Assert.Fail(rule + SourceTree.Report(added));
        }

        if (fixedEntries.Count > 0)
        {
            Assert.Fail(
                "These baseline entries no longer break the rule; strike them from the baseline so it cannot grow back:"
                + SourceTree.Report(fixedEntries));
        }
    }

    public static void None(IEnumerable<string> offenders, string rule) => Hold(offenders, [], rule);
}
