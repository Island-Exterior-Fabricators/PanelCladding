using System.Text.RegularExpressions;

namespace RepoCheck;

public static class ProseRules
{
    public const string TwoSentencesRule = "two sentences on one line";
    public const string WrappedRule = "sentence wrapped onto the next line";

    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "e.g", "i.e", "etc", "vs", "cf", "approx", "no", "fig", "mr", "ms", "dr", "st",
    };

    private static readonly Regex Fence = new(@"^\s*(```|~~~)", RegexOptions.Compiled);
    private static readonly Regex ListMarker = new(@"^\s*(?:[-*+]|\d+[.)])\s+(?:\[[ xX]\]\s+)?", RegexOptions.Compiled);
    private static readonly Regex InlineCode = new("`[^`]*`", RegexOptions.Compiled);
    private static readonly Regex LinkTarget = new(@"\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex SentenceBreak = new(@"([\w.]*?)([.!?])[""')\]*_]*\s+(?=[""'(\[*_`]*[A-Z0-9])", RegexOptions.Compiled);
    private static readonly Regex SentenceEnd = new(@"[.!?:][""')\]*_`]*$", RegexOptions.Compiled);
    private static readonly Regex Rule = new(@"^\s*(?:-{3,}|\*{3,}|_{3,}|={3,})\s*$", RegexOptions.Compiled);

    public static IEnumerable<Finding> Find(string path, string text, FileKind kind)
    {
        bool plainText = kind == FileKind.PlainText;
        string[] lines = CommentRules.Lines(text).Select(line => line.Text).ToArray();
        bool inFence = false;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (Fence.IsMatch(line))
            {
                inFence = !inFence;
                continue;
            }
            if (inFence || IsExempt(line, plainText)) continue;
            if (HasTwoSentences(line))
                yield return new Finding(path, index + 1, TwoSentencesRule, "put each sentence on its own line: " + Shorten(line));
            else if (index + 1 < lines.Length && !EndsSentence(line) && ContinuesBlock(lines[index + 1], plainText))
                yield return new Finding(path, index + 1, WrappedRule, "join the sentence onto one line: " + Shorten(line));
        }
    }

    public static bool IsExempt(string line, bool plainText)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0 || Rule.IsMatch(trimmed)) return true;
        if (plainText) return IsPlainHeading(trimmed);
        return trimmed.StartsWith('#') || trimmed.StartsWith('|') || trimmed.StartsWith('<') || trimmed.StartsWith('>');
    }

    public static bool HasTwoSentences(string line)
    {
        string text = Mask(ListMarker.Replace(line, string.Empty, 1));
        foreach (Match match in SentenceBreak.Matches(text))
        {
            string word = (match.Groups[1].Value + match.Groups[2].Value).TrimEnd('.', '!', '?');
            if (!IsAbbreviationOrInitial(word)) return true;
        }
        return false;
    }

    public static IReadOnlyList<int> SentenceBreaks(string text)
    {
        string masked = Mask(text);
        var breaks = new List<int>();
        foreach (Match match in SentenceBreak.Matches(masked))
        {
            string word = (match.Groups[1].Value + match.Groups[2].Value).TrimEnd('.', '!', '?');
            if (!IsAbbreviationOrInitial(word)) breaks.Add(match.Index + match.Length);
        }
        return breaks;
    }

    public static bool EndsSentence(string line) => SentenceEnd.IsMatch(Mask(line.TrimEnd()).TrimEnd());

    public static bool ContinuesBlock(string next, bool plainText)
    {
        string trimmed = next.Trim();
        if (trimmed.Length == 0 || Fence.IsMatch(next) || ListMarker.IsMatch(next) || Rule.IsMatch(trimmed)) return false;
        if (plainText) return !IsPlainHeading(trimmed);
        return !(trimmed.StartsWith('#') || trimmed.StartsWith('|') || trimmed.StartsWith('<') || trimmed.StartsWith('>'));
    }

    private static bool IsPlainHeading(string trimmed) =>
        trimmed.Any(char.IsLetter) && !trimmed.Any(char.IsLower);

    private static bool IsAbbreviationOrInitial(string word) =>
        Abbreviations.Contains(word) || word.Length == 1 && char.IsLetter(word[0]);

    private static string Mask(string text)
    {
        string masked = InlineCode.Replace(text, match => "`" + new string('X', match.Length - 2) + "`");
        return LinkTarget.Replace(masked, match => "]" + new string('X', match.Length - 1));
    }

    private static string Shorten(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Length > 100 ? trimmed[..100] + "..." : trimmed;
    }
}
