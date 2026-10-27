extern alias rhinocommon;

using PanelCladdingEditor.Application.Services.PanelCladding;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectEnumeratorSettings = rhinocommon::Rhino.DocObjects.ObjectEnumeratorSettings;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

// Per-document frame configuration codes in the project's Frame-Config format
// (LivePanelNamingFormat). The registry is stored in the document string
// PanelCladdingEditor.FrameConfigCodes, keeps every code ever assigned, and is seeded from the
// sheet's Frame-Config rows and from panels that already carry a CW_1.08 code with its UD_FRAME_CONFIG
// (or legacy CW_9.02) configuration. Codes that do not fit the format were assigned under another one and are skipped.
internal static class LivePanelFrameConfigCodes
{
    public const string DocumentKey = "PanelCladdingEditor.FrameConfigCodes";
    private static readonly Dictionary<uint, PanelFrameConfigCodeRegistry> Registries = new();
    private static readonly HashSet<uint> ReportedForeignCodes = new();
    private static readonly PanelCladdingKeyService Keys = new();

    // Writes the canonical UD_FRAME_CONFIG configuration and its CW_1.08 code on proposed attributes.
    internal static bool Normalize(RhinoDoc document, ObjectAttributes attributes)
    {
        string config = ReadCanonical(attributes);
        if (config.Length == 0)
        {
            return false;
        }
        PanelFrameConfigCodeRegistry registry = ForDocument(document);
        // Without a recorded format a code of another shape (XYZ's one-letter "A" while the sheet
        // cannot be read) is kept rather than recoded in the legacy two-letter format.
        string current = attributes.GetUserString(PanelCladdingKeyService.FrameConfigKey)?.Trim() ?? string.Empty;
        if (!LivePanelNamingFormat.IsRecorded(document) && !registry.IsCode(current) && PanelNamingFormat.LooksLikeCode(current))
        {
            if (ReportedForeignCodes.Add(document.RuntimeSerialNumber))
            {
                RhinoApp.WriteLine($"PanelCladdingEditor: {PanelCladdingKeyService.FrameConfigKey} '{current}' does not fit the " +
                    $"{registry.Token} frame-config format and is left as it is. Run PCtemplatePNL to record the project's PNL format.");
            }
            return SetCanonical(attributes, PanelCladdingKeyService.FrameConfigBackupKey, config);
        }
        var code = registry.Resolve(config, out bool assigned);
        if (!code.Success || code.Data is null)
        {
            RhinoApp.WriteLine($"PanelCladdingEditor: {code.Message}");
            return false;
        }
        if (assigned)
        {
            document.Strings.SetString(DocumentKey, registry.ToJson());
            LivePanelNamingFormat.Mirror(document);
        }
        bool changed = SetCanonical(attributes, PanelCladdingKeyService.FrameConfigBackupKey, config);
        changed |= SetCanonical(attributes, PanelCladdingKeyService.FrameConfigKey, code.Data);
        return changed;
    }

    // Rebuilt when the project's Frame-Config format differs from the cached registry's.
    internal static PanelFrameConfigCodeRegistry ForDocument(RhinoDoc document)
    {
        string token = LivePanelNamingFormat.ForDocument(document).FrameConfig;
        if (Registries.TryGetValue(document.RuntimeSerialNumber, out PanelFrameConfigCodeRegistry? cached) && cached.Token == token)
        {
            return cached;
        }
        var registry = new PanelFrameConfigCodeRegistry(token);
        foreach ((string code, string config) in LivePanelNamingFormat.SheetFrameConfigs(document))
        {
            if (PanelFrameConfigFormat.IsFormatted(config)) registry.Register(code, config);
        }
        var stored = PanelFrameConfigCodeRegistry.FromJson(document.Strings.GetValue(DocumentKey), token);
        if (!stored.Success)
        {
            RhinoApp.WriteLine($"PanelCladdingEditor: {stored.Message}; rebuilding frame configuration codes from panels.");
        }
        foreach ((string code, string config) in stored.Data?.Entries ?? new Dictionary<string, string>())
        {
            registry.Register(code, config);
        }
        foreach (var rhinoObject in document.Objects.GetObjectList(new ObjectEnumeratorSettings
                 {
                     NormalObjects = true, LockedObjects = true, HiddenObjects = true,
                     ActiveObjects = true, ReferenceObjects = true
                 }))
        {
            string? code = rhinoObject.Attributes.GetUserString(PanelCladdingKeyService.FrameConfigKey)?.Trim();
            string config = PanelCladdingKeyService.ReadUserDefinedValue(
                LivePanelCladdingCidService.Read(rhinoObject.Attributes), PanelCladdingKeyService.FrameConfigBackupKey).Trim();
            if (registry.IsCode(code) && PanelFrameConfigFormat.IsFormatted(config))
            {
                registry.Register(code!, config!);
            }
        }
        Registries[document.RuntimeSerialNumber] = registry;
        return registry;
    }

    internal static void Forget(uint documentRuntimeSerialNumber)
    {
        Registries.Remove(documentRuntimeSerialNumber);
        ReportedForeignCodes.Remove(documentRuntimeSerialNumber);
    }

    internal static void ForgetAll()
    {
        Registries.Clear();
        ReportedForeignCodes.Clear();
    }

    private static string ReadCanonical(ObjectAttributes attributes)
    {
        var canonical = Keys.ReadCanonicalFrameConfiguration(LivePanelCladdingCidService.Read(attributes));
        if (!canonical.Success)
        {
            RhinoApp.WriteLine($"PanelCladdingEditor: frame configuration not coded: {canonical.Message}");
        }
        return canonical.Success ? canonical.Data ?? string.Empty : string.Empty;
    }

    private static bool SetCanonical(ObjectAttributes attributes, string key, string value)
    {
        string?[] variants = (attributes.GetUserStrings()?.AllKeys ?? Array.Empty<string?>())
            .Where(existing => existing is not null && string.Equals(existing, key, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (variants.Length == 1 && variants[0] == key && attributes.GetUserString(key) == value)
        {
            return false;
        }
        foreach (string? variant in variants)
        {
            attributes.DeleteUserString(variant!);
        }
        attributes.SetUserString(key, value);
        return true;
    }
}
