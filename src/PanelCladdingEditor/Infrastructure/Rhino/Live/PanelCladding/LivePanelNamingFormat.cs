extern alias rhinocommon;

using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

internal static class LivePanelNamingFormat
{
    public const string DocumentKey = PanelNamingFormat.PropertyName;
    private static readonly OpenXmlPanelNamingConventionWorkbook Workbook = new();
    private static readonly Dictionary<uint, State> States = new();

    private sealed record State(PanelNamingFormat Format, bool Recorded, IReadOnlyDictionary<string, string> SheetFrameConfigs);

    internal static PanelNamingFormat ForDocument(RhinoDoc document) => Load(document).Format;

    internal static bool IsRecorded(RhinoDoc document) => Load(document).Recorded;

    internal static IReadOnlyDictionary<string, string> SheetFrameConfigs(RhinoDoc document) => Load(document).SheetFrameConfigs;

    internal static PanelNamingFormat Remember(RhinoDoc document, PanelNamingConventionSheetContent sheet)
    {
        PanelNamingFormat format = Parse(sheet.RecordedFormat) ?? PanelNamingFormat.Legacy;
        States[document.RuntimeSerialNumber] = new State(format, sheet.RecordedFormat.Length > 0, sheet.FrameConfigs);
        return format;
    }

    internal static void Mirror(RhinoDoc document)
    {
        State state = Load(document);
        if (state.Recorded && document.Strings.GetValue(DocumentKey) != state.Format.Serialize())
        {
            document.Strings.SetString(DocumentKey, state.Format.Serialize());
        }
    }

    internal static void Forget(uint documentRuntimeSerialNumber) => States.Remove(documentRuntimeSerialNumber);

    internal static void ForgetAll() => States.Clear();

    private static State Load(RhinoDoc document)
    {
        if (States.TryGetValue(document.RuntimeSerialNumber, out State? cached)) return cached;
        State state = ReadSheet(document) ?? FromMirror(document);
        States[document.RuntimeSerialNumber] = state;
        return state;
    }

    private static State? ReadSheet(RhinoDoc document)
    {
        string projectCode = PanelCladdingProjectService.CodeFromDocumentPath(document.Path);
        string? path = projectCode.Length == 0 ? null : LivePanelNamingConventionSync.ResolvePath(document, null, projectCode);
        if (path is null || !File.Exists(path)) return null;
        var sheet = Workbook.Read(path);
        if (!sheet.Success || sheet.Data is null)
        {
            RhinoApp.WriteLine($"PanelCladdingEditor: {sheet.Message}; using the PNL format stored in the model.");
            return null;
        }
        PanelNamingFormat format = Parse(sheet.Data.RecordedFormat) ?? PanelNamingFormat.Legacy;
        return new State(format, sheet.Data.RecordedFormat.Length > 0, sheet.Data.FrameConfigs);
    }

    private static State FromMirror(RhinoDoc document)
    {
        PanelNamingFormat? mirrored = Parse(document.Strings.GetValue(DocumentKey));
        return new State(mirrored ?? PanelNamingFormat.Legacy, mirrored is not null, new Dictionary<string, string>());
    }

    private static PanelNamingFormat? Parse(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : PanelNamingFormat.Parse(text).Data;
}
