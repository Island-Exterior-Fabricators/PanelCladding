namespace PanelCladdingEditor.Domain.Models.PanelCladding;

// One code row in a naming-convention table. Unused rows stay listed but are struck through.
public sealed record PanelNamingCodeRow(string Code, string Description, bool Used);

public sealed class PanelNamingConventionTables
{
    public IReadOnlyList<PanelNamingCodeRow> UnitTypes { get; init; } = Array.Empty<PanelNamingCodeRow>();
    public IReadOnlyList<PanelNamingCodeRow> FrameConfigs { get; init; } = Array.Empty<PanelNamingCodeRow>();
    public IReadOnlyList<PanelNamingCodeRow> WallTypes { get; init; } = Array.Empty<PanelNamingCodeRow>();
    // Project code for the Job Code cells of the example row; empty leaves them as they are.
    public string JobCode { get; init; } = string.Empty;
    // Wall-type values the model uses for which the wall format has no free code left.
    public IReadOnlyList<string> UncodedWallTypes { get; init; } = Array.Empty<string>();
}

// Code tables already present in the sheet (code -> description), read before rewriting it.
public sealed class PanelNamingConventionSheetContent
{
    public IReadOnlyDictionary<string, string> FrameConfigs { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> WallTypes { get; init; } = new Dictionary<string, string>();
    // Number of example-row cells under the "Job Code" header; 0 when the sheet has none.
    public int JobCodeWidth { get; init; }
    // The PNL format recorded by PCtemplatePNL (PanelNamingFormat text); empty when none is recorded.
    public string RecordedFormat { get; init; } = string.Empty;
}

// What the model currently uses.
public sealed class PanelNamingConventionUsage
{
    public IReadOnlySet<string> UnitTypes { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> FrameConfigCodes { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> WallTypes { get; init; } = new HashSet<string>();
}

// The PNL sheet as PCtemplatePNL finds it: the recorded format text and, per segment title
// (Wall-Type, Frame-Config, Frame-Type, Cladding, Definition), the codes that already have a value.
public sealed class PanelNamingTemplateState
{
    public string RecordedFormat { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, IReadOnlyList<string>> WrittenCodes { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();
}

public sealed class PanelNamingTemplateResult
{
    public bool Changed { get; init; }
    // What was configured or left alone, one line each, for the command line.
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
