using System.Globalization;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

// Builds the PNL sheet code tables from the document's codes, the sheet's existing rows and
// the model's usage, in the project's PNL format. Codes are never reassigned; rows nothing
// uses are kept but marked unused.
public static partial class PanelNamingConventionService
{
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

        // A new wall value takes the first code of the wall format without a value in the sheet,
        // which is the first placeholder row when the sheet has them.
        var wallTypes = new List<(string Code, string Value)>();
        var knownWallValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string code, string value) in sheet.WallTypes)
        {
            wallTypes.Add((code, value));
            knownWallValues.Add(value);
        }
        var usedCodes = wallTypes.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
        var uncoded = new List<string>();
        foreach (string value in usage.WallTypes.Where(value => !knownWallValues.Contains(value)).OrderBy(NaturalKey, StringComparer.Ordinal))
        {
            string? code = PanelNamingFormat.Sequence(format.WallType).FirstOrDefault(candidate => !usedCodes.Contains(candidate));
            if (code is null)
            {
                uncoded.Add(value);
                continue;
            }
            usedCodes.Add(code);
            wallTypes.Add((code, value));
        }

        return new PanelNamingConventionTables
        {
            UnitTypes = UnitTypeCodes
                .Select(item => new PanelNamingCodeRow(item.Code, item.Description, usage.UnitTypes.Contains(item.Code)))
                .ToArray(),
            FrameConfigs = frameConfigs
                .Select(item => new PanelNamingCodeRow(item.Key, item.Value, usage.FrameConfigCodes.Contains(item.Key)))
                .ToArray(),
            WallTypes = wallTypes
                .OrderBy(item => PanelNamingFormat.SequenceIndex(format.WallType, item.Code))
                .Select(item => new PanelNamingCodeRow(item.Code, item.Value,
                    usage.WallTypes.Contains(item.Value, StringComparer.OrdinalIgnoreCase)))
                .ToArray(),
            JobCode = (jobCode ?? string.Empty).Trim().ToUpperInvariant(),
            UncodedWallTypes = uncoded
        };
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
