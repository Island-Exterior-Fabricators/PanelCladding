namespace PanelCladdingEditor.Application.Services.PanelCladding;

public static class PanelCladdingProjectService
{
    public const string RootLayerSuffix = "-IEF-Wireframe";
    public const string MainModelNameSuffix = " - Wireframe";

    public static string CodeFromDocumentPath(string? documentPath)
    {
        string name = Path.GetFileNameWithoutExtension((documentPath ?? string.Empty).Trim());
        int separator = name.IndexOf(" - ", StringComparison.Ordinal);
        string head = (separator > 0 ? name[..separator] : name).Trim();
        return new string(head.TakeWhile(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

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

    public static string PanelLayerScope(string? projectCode) =>
        string.IsNullOrWhiteSpace(projectCode)
            ? PanelCladdingExtrusionPlanningService.PanelSurfaceLayerPattern
            : RootLayerName(projectCode) + "::" + PanelCladdingExtrusionPlanningService.PanelSurfaceLayerName;
}
