extern alias rhinocommon;

using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using DocumentEventArgs = rhinocommon::Rhino.DocumentEventArgs;
using DocumentSaveEventArgs = rhinocommon::Rhino.DocumentSaveEventArgs;
using ObjectEnumeratorSettings = rhinocommon::Rhino.DocObjects.ObjectEnumeratorSettings;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

// Updates the PNL sheet of "<PROJECT> - Naming Convention.xlsx" after every save of the main
// model "<PROJECT> - Wireframe.3dm". Other models of the project only read the sheet (format and
// reserved codes). The model keeps the codes (CW_1.08 + UD_FRAME_CONFIG backup), so a locked sheet only
// delays the update to the next save. The document is never modified from here.
internal static class LivePanelNamingConventionSync
{
    public const string PathDocumentKey = "PanelCladdingEditor.NamingConventionPath";
    public const string FileNameSuffix = " - Naming Convention.xlsx";
    private static readonly OpenXmlPanelNamingConventionWorkbook Workbook = new();
    private static readonly HashSet<uint> ReportedMissing = new();
    private static readonly HashSet<uint> ReportedJobCode = new();
    private static readonly HashSet<uint> ReportedSecondaryModel = new();
    private static bool _registered;

    internal static void Register()
    {
        if (_registered) return;
        _registered = true;
        RhinoDoc.EndSaveDocument += OnEndSaveDocument;
        RhinoDoc.CloseDocument += OnCloseDocument;
    }

    private static void OnCloseDocument(object? sender, DocumentEventArgs args)
    {
        LivePanelFrameConfigCodes.Forget(args.DocumentSerialNumber);
        LivePanelNamingFormat.Forget(args.DocumentSerialNumber);
        ReportedMissing.Remove(args.DocumentSerialNumber);
        ReportedJobCode.Remove(args.DocumentSerialNumber);
        ReportedSecondaryModel.Remove(args.DocumentSerialNumber);
    }

    private static void OnEndSaveDocument(object? sender, DocumentSaveEventArgs args)
    {
        try
        {
            if (args.ExportSelected || args.Document is not RhinoDoc document || IsAutoSave(args.FileName)) return;
            Sync(document, args.FileName);
        }
        catch (Exception exception)
        {
            RhinoApp.WriteLine($"PanelCladdingEditor: naming convention sheet not updated: {exception.Message}");
        }
    }

    private static void Sync(RhinoDoc document, string? savedPath)
    {
        // The project comes from the saved file name: BKT - Wireframe.3dm is BKT, its panels live under
        // BKT-IEF-Wireframe and its sheet is BKT - Naming Convention.xlsx with Job Code BKT.
        string modelPath = (string.IsNullOrWhiteSpace(savedPath) ? document.Path : savedPath) ?? string.Empty;
        string projectCode = PanelCladdingProjectService.CodeFromDocumentPath(modelPath);
        if (projectCode.Length == 0) return;
        // The sheet is read first: its recorded PNL format decides which codes are valid.
        string? path = ResolvePath(document, savedPath, projectCode);
        bool exists = path is not null && File.Exists(path);
        var sheet = exists ? Workbook.Read(path!) : null;
        PanelNamingFormat format = sheet is { Success: true, Data: not null }
            ? LivePanelNamingFormat.Remember(document, sheet.Data)
            : LivePanelNamingFormat.ForDocument(document);
        PanelFrameConfigCodeRegistry registry = LivePanelFrameConfigCodes.ForDocument(document);
        Snapshot? snapshot = ReadModel(document, registry, projectCode);
        if (snapshot is null) return;
        if (snapshot.MismatchedCodes > 0)
        {
            RhinoApp.WriteLine($"PanelCladdingEditor: {snapshot.MismatchedCodes} panel(s) have a {PanelCladdingKeyService.FrameConfigKey} " +
                $"code that does not match {PanelCladdingKeyService.FrameConfigBackupKey}; the layout follows {PanelCladdingKeyService.FrameConfigBackupKey} " +
                "and the code is corrected on the next panel write. Use PCmatchCrv or PCeditor to change a frame configuration.");
        }
        if (!exists || sheet is null)
        {
            if (ReportedMissing.Add(document.RuntimeSerialNumber))
            {
                RhinoApp.WriteLine($"PanelCladdingEditor: naming convention sheet not found ({path ?? snapshot.ProjectCode + FileNameSuffix}); codes stay in the model.");
            }
            return;
        }
        ReportedMissing.Remove(document.RuntimeSerialNumber);
        if (!sheet.Success || sheet.Data is null)
        {
            RhinoApp.WriteLine($"PanelCladdingEditor: {sheet.Message}");
            return;
        }
        // Codes listed only in the sheet stay reserved for this session's new assignments. A sheet
        // row that gives a code another configuration is never overwritten; it is reported.
        foreach ((string code, string config) in sheet.Data.FrameConfigs)
        {
            if (!PanelFrameConfigFormat.IsFormatted(config) || registry.Register(code, config).Success) continue;
            string modelConfig = registry.TryGetConfig(code, out string known) ? known : string.Empty;
            RhinoApp.WriteLine($"PanelCladdingEditor: {Path.GetFileName(path)} lists Frame-Config {code} as '{config}' but the model " +
                (modelConfig.Length > 0 ? $"uses {code} for '{modelConfig}'" : $"codes '{config}' differently") + "; the sheet row is left as it is.");
        }
        // Only the main model writes the sheet: another model of the project (MET - Wireframe_STN.3dm)
        // holds a subset of the panels and would strike or add codes for that subset alone.
        if (!PanelCladdingProjectService.IsMainModelPath(modelPath))
        {
            if (ReportedSecondaryModel.Add(document.RuntimeSerialNumber))
            {
                RhinoApp.WriteLine($"PanelCladdingEditor: {Path.GetFileName(path)} is updated only when " +
                    $"{PanelCladdingProjectService.MainModelFileName(projectCode)} is saved; {Path.GetFileName(modelPath)} only reads it.");
            }
            return;
        }
        string jobCode = snapshot.ProjectCode;
        if (sheet.Data.JobCodeWidth > 0 && !PanelNamingConventionService.JobCodeFits(jobCode, sheet.Data.JobCodeWidth) &&
            ReportedJobCode.Add(document.RuntimeSerialNumber))
        {
            RhinoApp.WriteLine($"PanelCladdingEditor: project code {jobCode} does not fit the {sheet.Data.JobCodeWidth} Job Code cells of {Path.GetFileName(path)}; Job Code left unchanged.");
        }
        PanelNamingConventionTables tables = PanelNamingConventionService.BuildTables(registry.Entries, sheet.Data, snapshot.Usage, jobCode, format);
        if (tables.UncodedWallTypes.Count > 0)
        {
            RhinoApp.WriteLine($"PanelCladdingEditor: no free {format.WallType} Wall-Type code for {string.Join(", ", tables.UncodedWallTypes)}; " +
                "run PCtemplatePNL with a wider Wall-Type format.");
        }
        if (tables.UncodedFrameTypes.Count > 0)
        {
            RhinoApp.WriteLine($"PanelCladdingEditor: no free {format.FrameType} Frame-Type code for {string.Join(", ", tables.UncodedFrameTypes)}; " +
                "run PCtemplatePNL with a wider Frame-Type format.");
        }
        var written = Workbook.Write(path!, tables);
        if (written.Success && written.Message == OpenXmlPanelNamingConventionWorkbook.UnchangedMessage) return;
        RhinoApp.WriteLine(written.Success
            ? $"PanelCladdingEditor: {Path.GetFileName(path)} PNL updated ({tables.FrameConfigs.Count} frame configs, {tables.WallTypes.Count} wall types, {tables.FrameTypes.Count} frame types)."
            : $"PanelCladdingEditor: {written.Message}");
    }

    private static Snapshot? ReadModel(RhinoDoc document, PanelFrameConfigCodeRegistry registry, string projectCode)
    {
        var unitTypes = new HashSet<string>(StringComparer.Ordinal);
        var frameCodes = new HashSet<string>(StringComparer.Ordinal);
        var wallTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var frameTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool blankFrameType = false;
        int panels = 0;
        int mismatched = 0;
        foreach (var rhinoObject in document.Objects.GetObjectList(new ObjectEnumeratorSettings
                 {
                     NormalObjects = true, LockedObjects = true, HiddenObjects = true,
                     ActiveObjects = true, ReferenceObjects = false,
                     ObjectTypeFilter = ObjectType.Surface | ObjectType.Brep
                 }))
        {
            int layerIndex = rhinoObject.Attributes.LayerIndex;
            string layerPath = layerIndex >= 0 ? document.Layers[layerIndex]?.FullPath ?? string.Empty : string.Empty;
            if (!PanelCladdingExtrusionPlanningService.IsPanelLayerPath(layerPath, projectCode)) continue;
            panels++;

            var userText = LivePanelCladdingCidService.Read(rhinoObject.Attributes);
            string Value(string key) => (userText.FirstOrDefault(item =>
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty).Trim();
            string unitType = Value(PanelCladdingCidService.UnitTypeUserTextKey).ToUpperInvariant();
            if (PanelNamingConventionService.UnitTypeCodes.Any(item => item.Code == unitType)) unitTypes.Add(unitType);
            // The configuration is authoritative: a panel uses the code of its UD_FRAME_CONFIG text.
            string frameCode = Value(PanelCladdingKeyService.FrameConfigKey);
            string config = PanelCladdingKeyService.ReadUserDefinedValue(userText, PanelCladdingKeyService.FrameConfigBackupKey).Trim();
            if (PanelFrameConfigFormat.IsFormatted(config) && registry.TryGetCode(config, out string configCode))
            {
                frameCodes.Add(configCode);
                if (registry.IsCode(frameCode) && frameCode != configCode) mismatched++;
            }
            else if (registry.IsCode(frameCode))
            {
                frameCodes.Add(frameCode);
            }
            string wallType = Value(PanelCladdingSpawnPlanningService.WallTypeUserTextKey);
            if (wallType.Length > 0) wallTypes.Add(wallType);
            // CW_1.09 is hand-filled; a leftover assignment payload of 1.2.4 or older is not a type.
            string frameType = Value(PanelCladdingKeyService.FrameTypeKey);
            if (frameType.Length == 0) blankFrameType = true;
            else if (!frameType.StartsWith('{')) frameTypes.Add(frameType);
        }
        if (panels == 0) return null;
        return new Snapshot(
            projectCode,
            new PanelNamingConventionUsage
            {
                UnitTypes = unitTypes, FrameConfigCodes = frameCodes, WallTypes = wallTypes,
                FrameTypes = frameTypes, HasBlankFrameType = blankFrameType
            },
            mismatched);
    }

    // An explicit document path wins; otherwise the sheet sits next to the model.
    internal static string? ResolvePath(RhinoDoc document, string? savedPath, string projectCode)
    {
        string? configured = document.Strings.GetValue(PathDocumentKey)?.Trim();
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        string? directory = Path.GetDirectoryName(string.IsNullOrWhiteSpace(savedPath) ? document.Path : savedPath);
        return string.IsNullOrWhiteSpace(directory) ? null : Path.Combine(directory, projectCode + FileNameSuffix);
    }

    private static bool IsAutoSave(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName) ||
        Path.GetFileName(fileName).StartsWith("RhinoAutosave", StringComparison.OrdinalIgnoreCase) ||
        fileName.Contains(Path.DirectorySeparatorChar + "AutoSave" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(Path.GetExtension(fileName), ".3dm", StringComparison.OrdinalIgnoreCase);

    private sealed record Snapshot(string ProjectCode, PanelNamingConventionUsage Usage, int MismatchedCodes);
}
