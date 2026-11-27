using System.Globalization;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public static partial class PanelCladdingAutoAttributeService
{
    public const string AreaUnitSuffix = " sqf";

    public static IReadOnlyList<string> Keys { get; } = Array.AsReadOnly(new[]
    {
        PanelCladdingKeyService.UnitDimensionKey,
        PanelCladdingKeyService.UnitWidthKey,
        PanelCladdingKeyService.UnitHeightKey,
        PanelCladdingKeyService.UnitAreaKey,
        PanelCladdingKeyService.UnitWeightKey,
        PanelCladdingKeyService.PlaneKey
    });

    public static bool IsInputKey(string key) =>
        string.Equals(key, PanelCladdingKeyService.PsfKey, StringComparison.OrdinalIgnoreCase) ||
        Keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));

    public static string FormatArea(double squareFeet) =>
        squareFeet.ToString("F3", CultureInfo.InvariantCulture) + AreaUnitSuffix;

    public static string FormatWeight(double weight) => weight.ToString("F3", CultureInfo.InvariantCulture);

    public static bool TryParsePsf(string? value, out double psf)
    {
        psf = 0;
        Match match = LeadingNumberRegex().Match(value ?? string.Empty);
        return match.Success &&
            double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out psf) &&
            double.IsFinite(psf);
    }

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

    public static IReadOnlyDictionary<string, string> CreateValues(
        double width, double height, double areaSquareFeet, IReadOnlyDictionary<string, string> userText,
        PanelPlaneFrame plane)
    {
        var values = new Dictionary<string, string>(CreateValues(width, height, areaSquareFeet, userText), StringComparer.Ordinal)
        {
            [PanelCladdingKeyService.PlaneKey] = PanelCladdingPlaneFormat.Format(plane)
        };
        return values;
    }

    public static IReadOnlyDictionary<string, string> Changes(
        IReadOnlyDictionary<string, string> desired, IReadOnlyDictionary<string, string> userText,
        double planeTolerance = 0)
    {
        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in desired)
        {
            KeyValuePair<string, string>[] stored = userText
                .Where(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)).ToArray();
            bool satisfied = string.IsNullOrWhiteSpace(value)
                ? stored.All(item => string.IsNullOrWhiteSpace(item.Value) && item.Key == key)
                : stored.Length == 1 && stored[0].Key == key && (stored[0].Value == value ||
                    (key == PanelCladdingKeyService.PlaneKey &&
                     PanelCladdingPlaneFormat.TryParse(value, out PanelPlaneFrame frame) &&
                     PanelCladdingPlaneFormat.Matches(stored[0].Value, frame, planeTolerance)));
            if (!satisfied) changes[key] = value;
        }
        return changes;
    }

    [GeneratedRegex(@"^\s*[-+]?(?:\d+(?:\.\d*)?|\.\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumberRegex();
}
