namespace RepoCheck;

public static class Exclusions
{
    public static readonly IReadOnlyList<string> CommentRulePaths = [];

    public static readonly IReadOnlyList<string> ProseRulePaths = [];

    public static readonly IReadOnlyList<string> TimingRulePaths = ["tests/Live/"];

    public static bool Covers(IReadOnlyList<string> prefixes, string path) =>
        prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
