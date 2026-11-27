namespace PanelCladdingEditor.Domain.Models.PanelCladding;

public sealed record PanelNamingCodeRow(string Code, string Description, bool Used);

public sealed class PanelNamingConventionTables
{
    public IReadOnlyList<PanelNamingCodeRow> UnitTypes { get; init; } = Array.Empty<PanelNamingCodeRow>();
    public IReadOnlyList<PanelNamingCodeRow> FrameConfigs { get; init; } = Array.Empty<PanelNamingCodeRow>();
    public IReadOnlyList<PanelNamingCodeRow> WallTypes { get; init; } = Array.Empty<PanelNamingCodeRow>();
    public IReadOnlyList<PanelNamingCodeRow> FrameTypes { get; init; } = Array.Empty<PanelNamingCodeRow>();
    public string JobCode { get; init; } = string.Empty;
    public IReadOnlyList<string> UncodedWallTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UncodedFrameTypes { get; init; } = Array.Empty<string>();
}

public sealed class PanelNamingConventionSheetContent
{
    public IReadOnlyDictionary<string, string> FrameConfigs { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> WallTypes { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> FrameTypes { get; init; } = new Dictionary<string, string>();
    public int JobCodeWidth { get; init; }
    public string RecordedFormat { get; init; } = string.Empty;
}

public sealed class PanelNamingConventionUsage
{
    public IReadOnlySet<string> UnitTypes { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> FrameConfigCodes { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> WallTypes { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> FrameTypes { get; init; } = new HashSet<string>();
    public bool HasBlankFrameType { get; init; }
}

public sealed class PanelNamingTemplateState
{
    public string RecordedFormat { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, IReadOnlyList<string>> WrittenCodes { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();
}

public sealed class PanelNamingTemplateResult
{
    public bool Changed { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
