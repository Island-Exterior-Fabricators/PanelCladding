using System.Text.RegularExpressions;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

// PCbake skips a panel with its own problem and marks it with a text dot, so the rest of the
// batch still bakes. A dot belongs to one panel through PanelUserTextKey; every run removes the
// dots of the panels it was given and adds new ones for the panels that are skipped again.
public static partial class PanelCladdingBakeIssueService
{
    public const string IssueLayerPath = "PCbake Issues";
    public const string PanelUserTextKey = "PCbake_Panel";
    public const string DetailUserTextKey = "PCbake_Issue";
    public static readonly PanelColorRgb IssueLayerColor = new(255, 0, 0);

    public const string MissingPanelStage = "Panel not found";
    public const string PidStage = "No PID";
    public const string NotEditableStage = "Locked or hidden";
    public const string DuplicateCidStage = "Duplicate CID";
    public const string PointOrderStage = "Point order";
    public const string SurfaceStage = "Surfaces";
    public const string CurveStage = "Curves";

    private const int MaxDetailLength = 80;

    public static string DotText(PanelCladdingBakeIssue issue)
    {
        string detail = ShortDetail(issue.Detail);
        return detail.Length == 0
            ? $"PCbake: {issue.Stage}"
            : $"PCbake: {issue.Stage}\n{detail}";
    }

    // Object ids and the PANEL_CLADDING_ prefix carry nothing for the reader of a dot.
    public static string ShortDetail(string detail)
    {
        string text = GuidPattern().Replace(detail ?? string.Empty, string.Empty);
        text = text.Replace("PANEL_CLADDING_", string.Empty, StringComparison.Ordinal);
        text = SeparatorRunPattern().Replace(text, string.Empty).Trim().Trim(':').Trim();
        return text.Length <= MaxDetailLength ? text : text[..(MaxDetailLength - 1)].TrimEnd() + "…";
    }

    // One issue per panel; the earliest stage wins because later stages never ran for it.
    public static IReadOnlyList<PanelCladdingBakeIssue> Merge(IEnumerable<PanelCladdingBakeIssue> issues) =>
        issues.Where(issue => issue.PanelObjectId != Guid.Empty)
            .GroupBy(issue => issue.PanelObjectId)
            .Select(group => group.First())
            .ToArray();

    public static bool IsDotOf(string? panelUserText, IReadOnlySet<Guid> panelObjectIds) =>
        Guid.TryParse(panelUserText?.Trim(), out Guid panel) && panelObjectIds.Contains(panel);

    [GeneratedRegex(@"\{?[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}?")]
    private static partial Regex GuidPattern();

    // A removed id leaves ": :"; a layer path's "::" has no space and is kept.
    [GeneratedRegex(@":\s+(?=:)")]
    private static partial Regex SeparatorRunPattern();
}
