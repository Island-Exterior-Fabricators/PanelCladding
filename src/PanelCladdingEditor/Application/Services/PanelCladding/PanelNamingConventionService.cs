using System.Globalization;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

// Builds the PNL sheet code tables from the document's codes, the sheet's existing rows and
// the model's usage, in the project's PNL format. Codes are never reassigned; rows nothing
// uses are kept but marked unused.
public static partial class PanelNamingConventionService
{
    public const string BlankFrameTypeValue = "NA";

    public static readonly IReadOnlyList<(string Code, string Description)> UnitTypeCodes =
    [
        (PanelCladdingCidService.CurvedUnitType, "Curved"),
        (PanelCladdingCidService.FlatUnitType, "Flat"),
        (PanelCladdingCidService.TwoFaceUnitType, "Two Face"),
        (PanelCladdingCidService.ThreeFaceUnitType, "Three Face")
    ];

    public static PanelNamingConventionTables BuildTables(
        IReadOnlyDictionary<string, string> frameConfigCodes,
        PanelNamingConventionSheetContent sheet,
        PanelNamingConventionUsage usage,
        string jobCode = "",
        PanelNamingFormat? format = null)
    {
        format ??= PanelNamingFormat.Legacy;
        var frameConfigs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach ((string code, string config) in frameConfigCodes)
        {
            frameConfigs[code] = config;
        }
        // Sheet-only rows stay reserved; rows that are not a frame configuration are dropped.
        foreach ((string code, string config) in sheet.FrameConfigs.Where(item => PanelFrameConfigFormat.IsFormatted(item.Value)))
        {
            frameConfigs.TryAdd(code, config);
        }

        (PanelNamingCodeRow[] wallTypes, string[] uncodedWallTypes) = CodeValues(
            sheet.WallTypes, usage.WallTypes, format.WallType,
            value => usage.WallTypes.Contains(value, StringComparer.OrdinalIgnoreCase));
        // A sheet row whose value is NA stands for a blank CW_1.09_FRAME_TYPE (XYZ's Frame-Type 01).
        (PanelNamingCodeRow[] frameTypes, string[] uncodedFrameTypes) = CodeValues(
            sheet.FrameTypes, usage.FrameTypes, format.FrameType,
            value => usage.FrameTypes.Contains(value, StringComparer.OrdinalIgnoreCase) ||
                (usage.HasBlankFrameType && string.Equals(value, BlankFrameTypeValue, StringComparison.OrdinalIgnoreCase)));

        return new PanelNamingConventionTables
        {
            UnitTypes = UnitTypeCodes
                .Select(item => new PanelNamingCodeRow(item.Code, item.Description, usage.UnitTypes.Contains(item.Code)))
                .ToArray(),
            FrameConfigs = frameConfigs
                .Select(item => new PanelNamingCodeRow(item.Key, item.Value, usage.FrameConfigCodes.Contains(item.Key)))
                .ToArray(),
            WallTypes = wallTypes,
            FrameTypes = frameTypes,
            JobCode = (jobCode ?? string.Empty).Trim().ToUpperInvariant(),
            UncodedWallTypes = uncodedWallTypes,
            UncodedFrameTypes = uncodedFrameTypes
        };
    }

    // Sheet rows keep their codes. A new value takes the first code of the format without a value
    // in the sheet, which is the first placeholder row when the sheet has them.
    private static (PanelNamingCodeRow[] Rows, string[] Uncoded) CodeValues(
        IReadOnlyDictionary<string, string> sheetRows, IReadOnlySet<string> values, string token, Func<string, bool> isUsed)
    {
        var rows = new List<(string Code, string Value)>();
        var knownValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string code, string value) in sheetRows)
        {
            rows.Add((code, value));
            knownValues.Add(value);
        }
        var usedCodes = rows.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
        var uncoded = new List<string>();
        foreach (string value in values.Where(value => !knownValues.Contains(value)).OrderBy(NaturalKey, StringComparer.Ordinal))
        {
            string? code = PanelNamingFormat.Sequence(token).FirstOrDefault(candidate => !usedCodes.Contains(candidate));
            if (code is null)
            {
                uncoded.Add(value);
                continue;
            }
            usedCodes.Add(code);
            rows.Add((code, value));
        }
        return (rows
                .OrderBy(item => PanelNamingFormat.SequenceIndex(token, item.Code))
                .Select(item => new PanelNamingCodeRow(item.Code, item.Value, isUsed(item.Value)))
                .ToArray(),
            uncoded.ToArray());
    }

    // The project code fills the Job Code cells one character each, or a single cell whole.
    public static bool JobCodeFits(string jobCode, int width) =>
        jobCode.Length > 0 && (width == 1 || width == jobCode.Length);

    // WT-2 sorts before WT-10.
    private static string NaturalKey(string value) =>
        DigitsRegex().Replace(value.ToUpperInvariant(), match => match.Value.PadLeft(8, '0'));

    [GeneratedRegex("[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex DigitsRegex();
}
