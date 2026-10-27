using System.Globalization;
using System.Text.RegularExpressions;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

// Values refreshed on every panel of the Surfaces - PNL branch (formerly the AutoATT script on
// AUTO_panel=1 panels): unit width, height and dimension in model units, area in square feet and
// weight = CW_6.01_PSF x area. Measurement happens in the Rhino adapter; this only formats.
public static partial class PanelCladdingAutoAttributeService
{
    public const string AreaUnitSuffix = " sqf";

    public static IReadOnlyList<string> Keys { get; } = Array.AsReadOnly(new[]
    {
        PanelCladdingKeyService.UnitDimensionKey,
        PanelCladdingKeyService.UnitWidthKey,
        PanelCladdingKeyService.UnitHeightKey,
        PanelCladdingKeyService.UnitAreaKey,
        PanelCladdingKeyService.UnitWeightKey
    });

    // Keys whose change (besides geometry and layer) makes a panel's values stale.
    public static bool IsInputKey(string key) =>
        string.Equals(key, PanelCladdingKeyService.PsfKey, StringComparison.OrdinalIgnoreCase) ||
        Keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));

    public static string FormatArea(double squareFeet) =>
        squareFeet.ToString("F3", CultureInfo.InvariantCulture) + AreaUnitSuffix;

    public static string FormatWeight(double weight) => weight.ToString("F3", CultureInfo.InvariantCulture);

    // The leading number of the PSF value ("85", "85 psf", "12.5PSF"); false when blank or not numeric.
    public static bool TryParsePsf(string? value, out double psf)
    {
        psf = 0;
        Match match = LeadingNumberRegex().Match(value ?? string.Empty);
        return match.Success &&
            double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out psf) &&
            double.IsFinite(psf);
    }

    // Desired values; the weight is the persisted blank when the PSF is not available.
    public static IReadOnlyDictionary<string, string> CreateValues(
        double width, double height, double areaSquareFeet, IReadOnlyDictionary<string, string> userText)
    {
        string unitWidth = PanelCladdingKeyService.FormatUnitDimension(width);
        string unitHeight = PanelCladdingKeyService.FormatUnitDimension(height);
        string psfText = userText.FirstOrDefault(item =>
            string.Equals(item.Key, PanelCladdingKeyService.PsfKey, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(item.Value)).Value ?? string.Empty;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PanelCladdingKeyService.UnitDimensionKey] = unitWidth + "x" + unitHeight,
            [PanelCladdingKeyService.UnitWidthKey] = unitWidth,
            [PanelCladdingKeyService.UnitHeightKey] = unitHeight,
            [PanelCladdingKeyService.UnitAreaKey] = FormatArea(areaSquareFeet),
            [PanelCladdingKeyService.UnitWeightKey] = TryParsePsf(psfText, out double psf)
                ? FormatWeight(psf * areaSquareFeet)
                : PanelCladdingKeyService.PersistedBlankCellValue
        };
    }

    // Only keys whose stored value differs, or that are stored under another case. A blank
    // value is already satisfied by a missing or blank key.
    public static IReadOnlyDictionary<string, string> Changes(
        IReadOnlyDictionary<string, string> desired, IReadOnlyDictionary<string, string> userText)
    {
        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in desired)
        {
            KeyValuePair<string, string>[] stored = userText
                .Where(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)).ToArray();
            bool satisfied = string.IsNullOrWhiteSpace(value)
                ? stored.All(item => string.IsNullOrWhiteSpace(item.Value) && item.Key == key)
                : stored.Length == 1 && stored[0].Key == key && stored[0].Value == value;
            if (!satisfied) changes[key] = value;
        }
        return changes;
    }

    [GeneratedRegex(@"^\s*[-+]?(?:\d+(?:\.\d*)?|\.\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumberRegex();
}
