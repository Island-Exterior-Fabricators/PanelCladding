using System.Globalization;
using PanelCladdingEditor.Contracts.Responses;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

// Per-project PNL code format recorded by PCtemplatePNL in the naming-convention workbook
// (custom property PanelCladdingEditor.PNLFormat), so every model of a project shares it.
// Each segment uses one token: A (one letter), AA (two letters), A1 (letter + digit),
// 01 (two digits) or 001 (three digits). Letters I and O are never used. Definition is
// always 01 and, like every two-digit segment, continues 01..99, A1..Z9, AA..ZZ.
public sealed record PanelNamingFormat(string WallType, string FrameConfig, string FrameType, string Cladding, string Definition)
{
    public const string PropertyName = "PanelCladdingEditor.PNLFormat";
    public const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    public const string DefinitionToken = "01";
    // Placeholder rows PCtemplatePNL keeps under each table: as many as one-letter codes.
    public const int PlaceholderCount = 24;

    public static readonly IReadOnlyList<string> Tokens = ["AA", "A", "A1", "01", "001"];
    public static readonly IReadOnlyList<string> SegmentTitles = ["Wall-Type", "Frame-Config", "Frame-Type", "Cladding", "Definition"];

    // ABC's format; a sheet without a recorded format keeps using it.
    public static PanelNamingFormat Legacy { get; } = new("A", "AA", "01", "01", DefinitionToken);

    public IReadOnlyList<(string Title, string Token)> Segments =>
    [
        (SegmentTitles[0], WallType), (SegmentTitles[1], FrameConfig), (SegmentTitles[2], FrameType),
        (SegmentTitles[3], Cladding), (SegmentTitles[4], Definition)
    ];

    public string Serialize() => string.Join("; ", Segments.Select(segment => $"{segment.Title}={segment.Token}"));

    public static OperationResponse<PanelNamingFormat> Parse(string? text)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string part in (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            string? title = SegmentTitles.FirstOrDefault(item => pair.Length == 2 && Key(item) == Key(pair[0]));
            if (title is null || !IsToken(pair[1]) || !tokens.TryAdd(title, pair[1]))
            {
                return OperationResponse<PanelNamingFormat>.Fail($"PANEL_NAMING_FORMAT_INVALID: '{text}'");
            }
        }
        tokens.TryAdd(SegmentTitles[4], DefinitionToken);
        return SegmentTitles.All(tokens.ContainsKey)
            ? OperationResponse<PanelNamingFormat>.Ok(new PanelNamingFormat(
                tokens[SegmentTitles[0]], tokens[SegmentTitles[1]], tokens[SegmentTitles[2]], tokens[SegmentTitles[3]], tokens[SegmentTitles[4]]))
            : OperationResponse<PanelNamingFormat>.Fail($"PANEL_NAMING_FORMAT_INVALID: '{text}'");
    }

    public static bool IsToken(string? token) => token is not null && Tokens.Contains(token);

    // User input ("a1", " 001 ") as a token, or null.
    public static string? ReadToken(string? input)
    {
        string token = (input ?? string.Empty).Trim().ToUpperInvariant();
        return IsToken(token) ? token : null;
    }

    // AA and A1 frame configuration codes are grouped: the first letter is the H/V class.
    public static bool IsGrouped(string token) => token is "AA" or "A1";

    // Codes in assignment order. Two-character tokens overflow without changing width:
    // 01..99, then A1..Z9, then AA..ZZ.
    public static IEnumerable<string> Sequence(string token) => token switch
    {
        "A" => Letters.Select(letter => letter.ToString()),
        "AA" => TwoLetters(),
        "A1" => LetterDigits().Concat(TwoLetters()),
        "01" => Enumerable.Range(1, 99).Select(value => value.ToString("00", CultureInfo.InvariantCulture))
            .Concat(LetterDigits()).Concat(TwoLetters()),
        "001" => Enumerable.Range(1, 999).Select(value => value.ToString("000", CultureInfo.InvariantCulture)),
        _ => []
    };

    public static bool Fits(string token, string? code) => code is not null && token switch
    {
        "A" => code.Length == 1 && IsLetter(code[0]),
        "AA" => IsTwoLetters(code),
        "A1" => IsLetterDigit(code) || IsTwoLetters(code),
        "01" => IsTwoDigits(code) || IsLetterDigit(code) || IsTwoLetters(code),
        "001" => code.Length == 3 && code.All(char.IsAsciiDigit) && code != "000",
        _ => false
    };

    // A code written under any format; used to leave codes of another format alone.
    public static bool LooksLikeCode(string? value) => Tokens.Any(token => Fits(token, value));

    public static int SequenceIndex(string token, string code)
    {
        int index = 0;
        foreach (string candidate in Sequence(token))
        {
            if (candidate == code) return index;
            index++;
        }
        return int.MaxValue;
    }

    private static IEnumerable<string> TwoLetters() =>
        Letters.SelectMany(first => Letters.Select(second => $"{first}{second}"));

    private static IEnumerable<string> LetterDigits() =>
        Letters.SelectMany(letter => "123456789".Select(digit => $"{letter}{digit}"));

    private static bool IsLetter(char value) => Letters.Contains(value);

    private static bool IsTwoLetters(string code) => code.Length == 2 && IsLetter(code[0]) && IsLetter(code[1]);

    private static bool IsLetterDigit(string code) => code.Length == 2 && IsLetter(code[0]) && code[1] is >= '1' and <= '9';

    private static bool IsTwoDigits(string code) => code.Length == 2 && code.All(char.IsAsciiDigit) && code != "00";

    private static string Key(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
