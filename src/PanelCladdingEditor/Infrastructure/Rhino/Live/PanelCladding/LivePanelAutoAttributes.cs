extern alias rhinocommon;

using PanelCladdingEditor.Application.Services.PanelCladding;
using AreaMassProperties = rhinocommon::Rhino.Geometry.AreaMassProperties;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Command = rhinocommon::Rhino.Commands.Command;
using DocumentEventArgs = rhinocommon::Rhino.DocumentEventArgs;
using DocumentOpenEventArgs = rhinocommon::Rhino.DocumentOpenEventArgs;
using Extrusion = rhinocommon::Rhino.Geometry.Extrusion;
using LayerTableEventArgs = rhinocommon::Rhino.DocObjects.Tables.LayerTableEventArgs;
using LayerTableEventType = rhinocommon::Rhino.DocObjects.Tables.LayerTableEventType;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectEnumeratorSettings = rhinocommon::Rhino.DocObjects.ObjectEnumeratorSettings;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoMath = rhinocommon::Rhino.RhinoMath;
using RhinoModifyObjectAttributesEventArgs = rhinocommon::Rhino.DocObjects.RhinoModifyObjectAttributesEventArgs;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using RhinoObjectEventArgs = rhinocommon::Rhino.DocObjects.RhinoObjectEventArgs;
using RhinoReplaceObjectEventArgs = rhinocommon::Rhino.DocObjects.RhinoReplaceObjectEventArgs;
using Surface = rhinocommon::Rhino.Geometry.Surface;
using UnitSystem = rhinocommon::Rhino.UnitSystem;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

// Always-on refresh of unit dimension, width, height, area, weight and the panel frame (UD_PLANE,
// computed from the geometry) on every panel of
// <PROJECT>-IEF-Wireframe::Surfaces - PNL and its sublayers (replaces the AutoATT script and its
// AUTO_panel=1 mark). Events only queue object ids; values are written on idle outside a command,
// because changing the document inside object events is unsafe. Undo is left alone: it restores
// the values recorded with the previous geometry.
internal static class LivePanelAutoAttributes
{
    private static readonly Dictionary<uint, HashSet<Guid>> Pending = new();
    private static readonly HashSet<uint> PendingScans = new();
    private static bool _registered;
    private static bool _writing;

    internal static void Register()
    {
        if (_registered) return;
        _registered = true;
        RhinoDoc.AddRhinoObject += OnAddRhinoObject;
        RhinoDoc.ReplaceRhinoObject += OnReplaceRhinoObject;
        RhinoDoc.ModifyObjectAttributes += OnModifyObjectAttributes;
        RhinoDoc.LayerTableEvent += OnLayerTableEvent;
        RhinoDoc.EndOpenDocument += OnEndOpenDocument;
        RhinoDoc.CloseDocument += OnCloseDocument;
        RhinoApp.Idle += OnIdle;
        if (RhinoDoc.ActiveDoc is RhinoDoc active) PendingScans.Add(active.RuntimeSerialNumber);
    }

    private static void OnAddRhinoObject(object? sender, RhinoObjectEventArgs args) =>
        Queue(args.TheObject?.Document, args.ObjectId);

    private static void OnReplaceRhinoObject(object? sender, RhinoReplaceObjectEventArgs args) =>
        Queue(args.Document, args.ObjectId);

    private static void OnModifyObjectAttributes(object? sender, RhinoModifyObjectAttributesEventArgs args)
    {
        if (args.OldAttributes is not ObjectAttributes before || args.NewAttributes is not ObjectAttributes after) return;
        if (before.LayerIndex != after.LayerIndex || InputsChanged(before, after))
        {
            Queue(args.Document, args.RhinoObject?.Id ?? Guid.Empty);
        }
    }

    // Renaming or moving a layer can bring panels into, or take them out of, the branch.
    private static void OnLayerTableEvent(object? sender, LayerTableEventArgs args)
    {
        if (_writing || args.EventType != LayerTableEventType.Modified || args.Document is not RhinoDoc document ||
            document.UndoActive || args.OldState is null || args.NewState is null) return;
        if (!string.Equals(args.OldState.Name, args.NewState.Name, StringComparison.Ordinal) ||
            args.OldState.ParentLayerId != args.NewState.ParentLayerId)
        {
            PendingScans.Add(document.RuntimeSerialNumber);
        }
    }

    private static void OnEndOpenDocument(object? sender, DocumentOpenEventArgs args)
    {
        if (args.Merge || args.Reference || args.Document is not RhinoDoc document) return;
        PendingScans.Add(document.RuntimeSerialNumber);
        Pending.Remove(document.RuntimeSerialNumber);
    }

    private static void OnCloseDocument(object? sender, DocumentEventArgs args)
    {
        Pending.Remove(args.DocumentSerialNumber);
        PendingScans.Remove(args.DocumentSerialNumber);
    }

    private static void Queue(RhinoDoc? document, Guid objectId)
    {
        if (_writing || document is null || objectId == Guid.Empty || document.UndoActive) return;
        if (!Pending.TryGetValue(document.RuntimeSerialNumber, out HashSet<Guid>? ids))
        {
            Pending[document.RuntimeSerialNumber] = ids = new HashSet<Guid>();
        }
        ids.Add(objectId);
    }

    private static bool InputsChanged(ObjectAttributes before, ObjectAttributes after)
    {
        var old = LivePanelCladdingCidService.Read(before);
        var current = LivePanelCladdingCidService.Read(after);
        return old.Keys.Concat(current.Keys)
            .Where(PanelCladdingAutoAttributeService.IsInputKey)
            .Any(key => !old.TryGetValue(key, out string? a) || !current.TryGetValue(key, out string? b) ||
                !string.Equals(a, b, StringComparison.Ordinal));
    }

    private static void OnIdle(object? sender, EventArgs args)
    {
        if ((Pending.Count == 0 && PendingScans.Count == 0) || _writing || Command.InCommand()) return;
        uint[] scans = PendingScans.ToArray();
        var batches = Pending.ToArray();
        PendingScans.Clear();
        Pending.Clear();
        _writing = true;
        try
        {
            foreach (uint serial in scans)
            {
                if (RhinoDoc.FromRuntimeSerialNumber(serial) is not RhinoDoc document) continue;
                RhinoObject[] panels = document.Objects.GetObjectList(new ObjectEnumeratorSettings
                {
                    NormalObjects = true, LockedObjects = true, HiddenObjects = true,
                    ActiveObjects = true, ReferenceObjects = false,
                    ObjectTypeFilter = ObjectType.Surface | ObjectType.Brep | ObjectType.Extrusion
                }).ToArray();
                int updated = panels.Count(panel => Refresh(document, panel));
                if (updated > 0)
                {
                    RhinoApp.WriteLine($"PanelCladdingEditor: unit dimension, area, weight and UD_PLANE refreshed on {updated} panel(s).");
                }
            }
            foreach ((uint serial, HashSet<Guid> ids) in batches)
            {
                if (scans.Contains(serial) || RhinoDoc.FromRuntimeSerialNumber(serial) is not RhinoDoc document) continue;
                foreach (Guid id in ids)
                {
                    if (document.Objects.FindId(id) is RhinoObject panel) Refresh(document, panel);
                }
            }
        }
        catch (Exception exception)
        {
            RhinoApp.WriteLine($"PanelCladdingEditor: panel attributes not refreshed: {exception.Message}");
        }
        finally
        {
            _writing = false;
        }
    }

    // True when the panel's values changed.
    private static bool Refresh(RhinoDoc document, RhinoObject panel)
    {
        if (panel.IsDeleted || panel.IsReference || panel.IsInstanceDefinitionGeometry) return false;
        int layerIndex = panel.Attributes.LayerIndex;
        string layerPath = layerIndex >= 0 ? document.Layers[layerIndex]?.FullPath ?? string.Empty : string.Empty;
        string project = PanelCladdingProjectService.CodeFromDocumentPath(document.Path);
        if (!PanelCladdingExtrusionPlanningService.IsPanelLayerPath(layerPath, project)) return false;
        Brep? brep = panel.Geometry switch
        {
            Brep value => value,
            Extrusion value => value.ToBrep(true),
            Surface value => value.ToBrep(),
            _ => null
        };
        if (brep is null) return false;

        var measured = LivePanelCladdingRepository.MeasurePanel(brep, document.ModelAbsoluteTolerance);
        using AreaMassProperties? mass = AreaMassProperties.Compute(brep, true, false, false, false);
        if (!measured.Success || mass is null || !double.IsFinite(mass.Area)) return false;
        double feet = RhinoMath.UnitScale(document.ModelUnitSystem, UnitSystem.Feet);
        var userText = LivePanelCladdingCidService.Read(panel.Attributes);
        var desired = PanelCladdingAutoAttributeService.CreateValues(
            measured.Data.Width, measured.Data.Height, mass.Area * feet * feet, userText, measured.Data.Plane);
        var changes = PanelCladdingAutoAttributeService.Changes(desired, userText, document.ModelAbsoluteTolerance);
        if (changes.Count == 0) return false;

        ObjectAttributes proposed = panel.Attributes.Duplicate();
        foreach ((string key, string value) in changes)
        {
            foreach (string variant in userText.Keys.Where(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase)))
            {
                proposed.DeleteUserString(variant);
            }
            proposed.SetUserString(key, value);
        }
        return document.Objects.ModifyAttributes(panel, proposed, true);
    }
}
