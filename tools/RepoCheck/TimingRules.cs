using System.Text.RegularExpressions;

namespace RepoCheck;

public static class TimingRules
{
    public const string Rule = "test waits for a duration or sets its own time limit";

    private static readonly (Regex Pattern, string Advice)[] PowerShellPatterns =
    [
        (new Regex(@"\bStart-Sleep\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "wait for an event or a process exit instead of sleeping"),
        (new Regex(@"\.WaitForExit\(\s*[^)\s]", RegexOptions.Compiled), "wait for the exit without a time limit"),
        (new Regex(@"\.WaitOne\(\s*[^)\s]", RegexOptions.Compiled), "wait for the event without a time limit"),
        (new Regex(@"\bWait-(?:Process|Job|Event)\b[^\r\n]*-Timeout\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "wait without a time limit"),
    ];

    public static bool Applies(string path, FileKind kind) =>
        kind == FileKind.PowerShell &&
        path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) &&
        !Exclusions.Covers(Exclusions.TimingRulePaths, path);

    public static IEnumerable<Finding> Find(string path, string text) =>
        from line in CommentRules.Lines(text)
        from rule in PowerShellPatterns
        where rule.Pattern.IsMatch(line.Text)
        select new Finding(path, line.Number, Rule, rule.Advice + ": " + line.Text.Trim());
}
