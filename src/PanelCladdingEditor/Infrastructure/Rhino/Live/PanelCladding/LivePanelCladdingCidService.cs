extern alias rhinocommon;

using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

internal static class LivePanelCladdingCidService
{
    internal static IReadOnlyDictionary<string, string> Read(ObjectAttributes attributes)
    {
        var text = attributes.GetUserStrings();
        return (text?.AllKeys ?? Array.Empty<string?>())
            .Where(key => key is not null)
            .ToDictionary(key => key!, key => text![key!] ?? string.Empty, StringComparer.Ordinal);
    }

    // Normalizes UD_ key names, the role case, panel identity (CID, name) and the frame
    // configuration code on a write.
    internal static bool Normalize(RhinoDoc document, ObjectAttributes attributes)
    {
        bool changed = MigrateUserDefinedKeys(attributes);
        changed |= NormalizeRole(attributes);
        changed |= LivePanelFrameConfigCodes.Normalize(document, attributes);
        IReadOnlyDictionary<string, string> text = Read(attributes);
        string? cid = PanelCladdingCidService.PanelCidWrite(text);
        if (cid is not null)
        {
            string key = PanelCladdingSpawnPlanningService.CidUserTextKey;
            string[] keys = text.Keys.Where(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (keys.Length != 1 || keys[0] != key || text[key] != cid)
            {
                foreach (string existing in keys)
                {
                    attributes.DeleteUserString(existing);
                }
                attributes.SetUserString(key, cid);
                changed = true;
            }
        }
        if (PanelCladdingCidService.PanelNameWrite(text) is string name &&
            !string.Equals(attributes.Name, name, StringComparison.Ordinal))
        {
            attributes.Name = name;
            changed = true;
        }
        return changed;
    }

    // A role in CW_5.00_DEFINITION_A is stored lowercase (Parent -> parent); other values stay.
    internal static bool NormalizeRole(ObjectAttributes attributes)
    {
        bool changed = false;
        foreach ((string key, string value) in Read(attributes).Where(item =>
                     string.Equals(item.Key, PanelCladdingCidService.RoleDefinitionKey, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            string role = PanelCladdingCidService.NormalizeRole(value);
            if (role == value) continue;
            attributes.SetUserString(key, role);
            changed = true;
        }
        return changed;
    }

    // CW_9.xx keys become UD_ keys on the panel's next write.
    internal static bool MigrateUserDefinedKeys(ObjectAttributes attributes)
    {
        var (deletes, writes) = PanelCladdingKeyService.PlanUserDefinedKeyMigration(Read(attributes));
        foreach (string key in deletes)
        {
            attributes.DeleteUserString(key);
        }
        foreach ((string key, string value) in writes)
        {
            attributes.SetUserString(key, value);
        }
        return deletes.Count > 0;
    }

    internal static OperationResponse<IReadOnlyList<Change>> Prepare(RhinoDoc document, IEnumerable<Guid> ids)
    {
        var changes = new List<Change>();
        foreach (Guid id in ids)
        {
            RhinoObject? panel = document.Objects.FindId(id);
            if (panel is null)
            {
                return OperationResponse<IReadOnlyList<Change>>.Fail($"PANEL_CLADDING_CID_PANEL_MISSING: {id:D}");
            }
            ObjectAttributes proposed = panel.Attributes.Duplicate();
            if (Normalize(document, proposed))
            {
                changes.Add(new Change(panel, panel.Attributes.Duplicate(), proposed));
            }
        }
        return OperationResponse<IReadOnlyList<Change>>.Ok(changes);
    }

    internal static OperationResponse Apply(RhinoDoc document, IReadOnlyList<Change> changes)
    {
        try
        {
            foreach (Change change in changes)
            {
                if (!document.Objects.ModifyAttributes(change.ObjectId, change.Proposed, quiet: true))
                {
                    return OperationResponse.Fail($"PANEL_CLADDING_CID_WRITE_FAILED: {change.ObjectId:D}");
                }
                change.Applied = true;
            }
            return OperationResponse.Ok();
        }
        catch (Exception ex)
        {
            return OperationResponse.Fail($"PANEL_CLADDING_CID_WRITE_FAILED: {ex.Message}");
        }
    }

    internal static bool Restore(RhinoDoc document, IReadOnlyList<Change> changes)
    {
        bool restored = true;
        foreach (Change change in changes.Reverse().Where(change => change.Applied))
        {
            restored &= document.Objects.ModifyAttributes(change.ObjectId, change.Original, quiet: true);
        }
        return restored;
    }

    internal sealed record Change(RhinoObject Panel, ObjectAttributes Original, ObjectAttributes Proposed)
    {
        // Geometry replacement can invalidate the original RhinoObject wrapper.
        public Guid ObjectId { get; } = Panel.Id;
        public bool Applied { get; set; }
    }
}
