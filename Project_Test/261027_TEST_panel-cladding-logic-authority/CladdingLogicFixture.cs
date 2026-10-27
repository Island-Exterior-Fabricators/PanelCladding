using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Application.Services.PanelCladding;

namespace PanelCladdingTestFixtures;

// Since 1.2.4 the plug-in reads cladding only from UD_CLADDING_LOGIC. Smokes written for the
// CW_4.xx read path link this file and pass their fixture user text through WithLogic, which stores
// the fixture's CW_4 values (every grid cell, blank when absent) as the logic, unvalidated, so
// fixtures that test invalid references still reach the plug-in's own checks.
internal static partial class CladdingLogicFixture
{
    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static Dictionary<string, string> WithLogic(IReadOnlyDictionary<string, string> userText)
    {
        var result = new Dictionary<string, string>(userText, StringComparer.OrdinalIgnoreCase);
        if (PanelCladdingKeyService.IsCladdingConfigured(userText) || !userText.Keys.Any(CellKeyRegex().IsMatch))
        {
            return result;
        }
        int horizontal = userText.Keys.Count(HorizontalRegex().IsMatch);
        int vertical = userText.Keys.Count(VerticalRegex().IsMatch);
        var logic = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int column = 0; column <= vertical; column++)
        {
            for (int row = 0; row <= horizontal; row++)
            {
                string rowLabel = PanelCladdingKeyService.GetRowLabel(row);
                string key = PanelCladdingKeyService.GetCellKey(column, rowLabel);
                string value = (userText.FirstOrDefault(item =>
                    string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty).Trim();
                logic[$"{column}{rowLabel}"] = PanelCladdingKeyService.IsCellLabelToken(value)
                    ? value.ToUpperInvariant()
                    : value;
            }
        }
        result[PanelCladdingKeyService.CladdingLogicKey] = JsonSerializer.Serialize(logic, Options);
        return result;
    }

    [GeneratedRegex(@"^CW_4\.\d{2}_CLADDING_\d+[A-Z]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CellKeyRegex();

    [GeneratedRegex(@"^CW_2\.03_OFFSET_H\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalRegex();

    [GeneratedRegex(@"^CW_2\.04_OFFSET_V\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VerticalRegex();
}
