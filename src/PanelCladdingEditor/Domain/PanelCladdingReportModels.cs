namespace PanelCladdingEditor.Domain.Models.PanelCladding;

public enum PanelCladdingReportSeverity
{
    Error,
    Warning,
    Info
}

public enum PanelCladdingReportCategory
{
    Layers,
    Attributes,
    Dependencies
}

public enum PanelCladdingReportObjectKind
{
    Brep,
    Curve
}

public sealed record PanelCladdingReportObject(
    Guid Id,
    string Name,
    string LayerPath,
    PanelCladdingReportObjectKind Kind,
    IReadOnlyDictionary<string, string> UserText);

public sealed class PanelCladdingReportSnapshot
{
    public string DocumentPath { get; init; } = string.Empty;
    public double Tolerance { get; init; } = 0.001d;
    public IReadOnlyList<string> LayerPaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PanelCladdingReportObject> Objects { get; init; } = Array.Empty<PanelCladdingReportObject>();
}

public sealed record PanelCladdingReportRule(
    string Id,
    string Title,
    PanelCladdingReportSeverity Severity,
    PanelCladdingReportCategory Category,
    string Fix);

public sealed record PanelCladdingReportIssue(
    PanelCladdingReportRule Rule,
    Guid ObjectId,
    string ObjectName,
    string LayerPath,
    string Detail);

public sealed class PanelCladdingReport
{
    public string DocumentPath { get; init; } = string.Empty;
    public string ProjectCode { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int PanelCount { get; init; }
    public int SurfaceCount { get; init; }
    public int CurveCount { get; init; }
    public IReadOnlyList<PanelCladdingReportIssue> Issues { get; init; } = Array.Empty<PanelCladdingReportIssue>();

    public int Count(PanelCladdingReportSeverity severity) =>
        Issues.Count(issue => issue.Rule.Severity == severity);
}
