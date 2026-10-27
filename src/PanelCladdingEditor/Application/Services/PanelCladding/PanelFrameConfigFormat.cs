using System.Globalization;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

// Readable, lossless frame configuration stored in UD_FRAME_CONFIG and the
// naming-convention sheet: "2H 1V | delete A1 0B | merge A0–1 | hide 0A".
// Horizontal segments are track letter + bay number and vertical segments track number +
// bay letter, matching the INT- curve codes; merge runs give the first and last bay.
public static partial class PanelFrameConfigFormat
{
    private const string SectionSeparator = " | ";
    private const char RangeSeparator = '–';

    public sealed record Parsed(int HorizontalTrackCount, int VerticalTrackCount, PanelCladdingTopologyState Topology);

    public static string Format(PanelCladdingTopologyState topology, int horizontalTrackCount, int verticalTrackCount)
    {
        var sections = new List<string>
        {
            $"{horizontalTrackCount.ToString(CultureInfo.InvariantCulture)}H " +
            $"{verticalTrackCount.ToString(CultureInfo.InvariantCulture)}V"
        };
        AddSection(sections, "delete", Ordered(topology.MissingSegments).Select(SegmentLabel));
        AddSection(sections, "merge", topology.MergeRuns.Distinct()
            .OrderBy(run => run.Axis).ThenBy(run => run.Track).ThenBy(run => run.StartBay)
            .Select(RunLabel));
        AddSection(sections, "hide", Ordered(topology.HiddenSegments).Select(SegmentLabel));
        return string.Join(SectionSeparator, sections);
    }

    public static bool IsFormatted(string? value) =>
        !string.IsNullOrWhiteSpace(value) && HeaderRegex().IsMatch(value.Split('|')[0].Trim());

    public static bool TryReadCounts(string? value, out int horizontalTrackCount, out int verticalTrackCount)
    {
        horizontalTrackCount = verticalTrackCount = 0;
        if (!IsFormatted(value)) return false;
        Match header = HeaderRegex().Match(value!.Split('|')[0].Trim());
        return int.TryParse(header.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out horizontalTrackCount) &&
            int.TryParse(header.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out verticalTrackCount);
    }

    public static OperationResponse<Parsed> Parse(string? value)
    {
        string[] sections = (value ?? string.Empty).Split('|').Select(part => part.Trim()).ToArray();
        if (!TryReadCounts(value, out int horizontal, out int vertical))
        {
            return Fail($"expected '<n>H <n>V' header, found '{value}'");
        }
        var missing = new List<PanelCladdingSegmentCoordinate>();
        var hidden = new List<PanelCladdingSegmentCoordinate>();
        var runs = new List<PanelCladdingMergeRun>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string section in sections.Skip(1))
        {
            Match match = SectionRegex().Match(section);
            if (!match.Success || !seen.Add(match.Groups[1].Value))
            {
                return Fail($"unknown or repeated section '{section}'");
            }
            foreach (string token in match.Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (match.Groups[1].Value == "merge")
                {
                    if (!TryParseRun(token, out PanelCladdingMergeRun? run)) return Fail($"invalid merge run '{token}'");
                    runs.Add(run!);
                }
                else
                {
                    if (!TryParseSegment(token, out PanelCladdingSegmentCoordinate segment)) return Fail($"invalid segment '{token}'");
                    (match.Groups[1].Value == "delete" ? missing : hidden).Add(segment);
                }
            }
        }
        return OperationResponse<Parsed>.Ok(new Parsed(horizontal, vertical, new PanelCladdingTopologyState
        {
            MissingSegments = Ordered(missing).ToArray(),
            HiddenSegments = Ordered(hidden).ToArray(),
            MergeRuns = runs.Distinct().OrderBy(run => run.Axis).ThenBy(run => run.Track).ThenBy(run => run.StartBay).ToArray()
        }));
    }

    private static void AddSection(List<string> sections, string name, IEnumerable<string> labels)
    {
        string joined = string.Join(' ', labels);
        if (joined.Length > 0) sections.Add($"{name} {joined}");
    }

    private static IEnumerable<PanelCladdingSegmentCoordinate> Ordered(IEnumerable<PanelCladdingSegmentCoordinate> segments) =>
        segments.Distinct().OrderBy(item => item.Axis).ThenBy(item => item.Track).ThenBy(item => item.Bay);

    private static string SegmentLabel(PanelCladdingSegmentCoordinate segment) =>
        segment.Axis == PanelCladdingTopologyAxis.Horizontal
            ? PanelCladdingExtrusionPlanningService.Alpha(segment.Track) + Number(segment.Bay)
            : Number(segment.Track) + PanelCladdingExtrusionPlanningService.Alpha(segment.Bay);

    private static string RunLabel(PanelCladdingMergeRun run) =>
        run.Axis == PanelCladdingTopologyAxis.Horizontal
            ? $"{PanelCladdingExtrusionPlanningService.Alpha(run.Track)}{Number(run.StartBay)}{RangeSeparator}{Number(run.EndBay)}"
            : $"{Number(run.Track)}{PanelCladdingExtrusionPlanningService.Alpha(run.StartBay)}{RangeSeparator}" +
              PanelCladdingExtrusionPlanningService.Alpha(run.EndBay);

    private static bool TryParseSegment(string token, out PanelCladdingSegmentCoordinate segment)
    {
        segment = default;
        Match horizontal = HorizontalSegmentRegex().Match(token);
        if (horizontal.Success)
        {
            segment = new(PanelCladdingTopologyAxis.Horizontal, AlphaIndex(horizontal.Groups[1].Value), int.Parse(horizontal.Groups[2].Value, CultureInfo.InvariantCulture));
            return true;
        }
        Match vertical = VerticalSegmentRegex().Match(token);
        if (!vertical.Success) return false;
        segment = new(PanelCladdingTopologyAxis.Vertical, int.Parse(vertical.Groups[1].Value, CultureInfo.InvariantCulture), AlphaIndex(vertical.Groups[2].Value));
        return true;
    }

    private static bool TryParseRun(string token, out PanelCladdingMergeRun? run)
    {
        run = null;
        Match horizontal = HorizontalRunRegex().Match(token);
        if (horizontal.Success)
        {
            run = new(PanelCladdingTopologyAxis.Horizontal, AlphaIndex(horizontal.Groups[1].Value),
                int.Parse(horizontal.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(horizontal.Groups[3].Value, CultureInfo.InvariantCulture));
            return true;
        }
        Match vertical = VerticalRunRegex().Match(token);
        if (!vertical.Success) return false;
        run = new(PanelCladdingTopologyAxis.Vertical, int.Parse(vertical.Groups[1].Value, CultureInfo.InvariantCulture),
            AlphaIndex(vertical.Groups[2].Value), AlphaIndex(vertical.Groups[3].Value));
        return true;
    }

    // Inverse of PanelCladdingExtrusionPlanningService.Alpha (A=0, Z=25, AA=26).
    private static int AlphaIndex(string letters)
    {
        int value = 0;
        foreach (char letter in letters) value = value * 26 + (letter - 'A' + 1);
        return value - 1;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static OperationResponse<Parsed> Fail(string reason) =>
        OperationResponse<Parsed>.Fail("PANEL_FRAME_CONFIG_TEXT_INVALID: " + reason);

    [GeneratedRegex(@"^([0-9]+)H ([0-9]+)V$", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"^(delete|merge|hide) (.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"^([A-Z]+)([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalSegmentRegex();

    [GeneratedRegex(@"^([0-9]+)([A-Z]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex VerticalSegmentRegex();

    [GeneratedRegex(@"^([A-Z]+)([0-9]+)[–-]([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalRunRegex();

    [GeneratedRegex(@"^([0-9]+)([A-Z]+)[–-]([A-Z]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex VerticalRunRegex();
}
