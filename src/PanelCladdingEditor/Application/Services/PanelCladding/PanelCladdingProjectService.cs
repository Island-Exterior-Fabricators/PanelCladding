namespace PanelCladdingEditor.Application.Services.PanelCladding;

// The project code comes from the Rhino file name: "ABC - Wireframe.3dm" is project ABC. It
// names the project root layer ABC-IEF-Wireframe and the Job Code of the naming-convention sheet.
public static class PanelCladdingProjectService
{
    public const string RootLayerSuffix = "-IEF-Wireframe";
    public const string MainModelNameSuffix = " - Wireframe";

    // The text before the first " - " (or the whole name), up to its first non-alphanumeric
    // character, in upper case. Empty for an unsaved document.
    public static string CodeFromDocumentPath(string? documentPath)
    {
        string name = Path.GetFileNameWithoutExtension((documentPath ?? string.Empty).Trim());
        int separator = name.IndexOf(" - ", StringComparison.Ordinal);
        string head = (separator > 0 ? name[..separator] : name).Trim();
        return new string(head.TakeWhile(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    // The project's main model is "<code> - Wireframe.3dm" (any case); only it updates the
    // naming-convention sheet. "XYZ - Wireframe_STN.3dm" and "ABC_old - Wireframe.3dm" are not.
    public static bool IsMainModelPath(string? documentPath)
    {
        string path = (documentPath ?? string.Empty).Trim();
        string code = CodeFromDocumentPath(path);
        return code.Length > 0 &&
               string.Equals(Path.GetExtension(path), ".3dm", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Path.GetFileNameWithoutExtension(path), code + MainModelNameSuffix, StringComparison.OrdinalIgnoreCase);
    }

    public static string MainModelFileName(string projectCode) => projectCode.Trim().ToUpperInvariant() + MainModelNameSuffix + ".3dm";

    public static string RootLayerName(string projectCode) => projectCode.Trim().ToUpperInvariant() + RootLayerSuffix;

    // "<code>-IEF-Wireframe::Surfaces - PNL", or the generic pattern when there is no code.
    public static string PanelLayerScope(string? projectCode) =>
        string.IsNullOrWhiteSpace(projectCode)
            ? PanelCladdingExtrusionPlanningService.PanelSurfaceLayerPattern
            : RootLayerName(projectCode) + "::" + PanelCladdingExtrusionPlanningService.PanelSurfaceLayerName;
}
