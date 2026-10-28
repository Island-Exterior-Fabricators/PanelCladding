using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingPidSetupService
{
    public static IReadOnlyList<string> RequiredKeys { get; } = Array.AsReadOnly(new[]
    {
        "CW_1.00_PNL",
        "CW_1.01_PID",
        "CW_1.02_CID",
        "CW_1.03_ELEVATION",
        "CW_1.04_LEVEL",
        "CW_1.05_LOT",
        "CW_1.06_UNIT_TYPE",
        "CW_1.07_WALL_TYPE",
        "CW_1.08_FRAME_CONFIG",
        "CW_1.09_FRAME_TYPE",
        "CW_1.10_CAD_TYPE",
        "CW_1.11_PHASE",
        PanelCladdingKeyService.UnitDimensionKey,
        PanelCladdingKeyService.UnitWidthKey,
        PanelCladdingKeyService.UnitHeightKey,
        "CW_2.05_ANCHOR_DL_LEFT",
        "CW_2.05_ANCHOR_DL_RIGHT",
        "CW_2.05_ANCHOR_WL_LEFT",
        "CW_2.05_ANCHOR_WL_RIGHT",
        "CW_2.06_PENETRATION_DIMENSION",
        "CW_5.00_DEFINITION_A",
        "CW_5.00_DEFINITION_B",
        "CW_5.00_DEFINITION_C",
        "CW_5.00_DEFINITION_D",
        "CW_6.00_WL_NEGATIVE",
        "CW_6.00_WL_POSITIVE",
        "CW_6.01_PSF",
        "CW_6.02_UNIT_WEIGHT",
        "CW_7.00_BLIND_SINGLE",
        "CW_7.00_BLIND_DOUBLE"
    });

    // Bounds are tight in the panel plane. Measure in that plane, not a world XY box.
    public OperationResponse<IReadOnlyDictionary<string, string>> CreateWrites(
        PanelCladdingPidAssignment assignment, PanelCladdingPidPanel panel)
    {
        if (assignment.ObjectId != panel.ObjectId || panel.Bounds.Count < 4 ||
            !Finite(panel.Normal) || panel.Bounds.Any(p => !Finite(p)))
            return Fail(panel, "Invalid panel dimensions input.");
        double normalLength = Math.Sqrt(Dot(panel.Normal, panel.Normal));
        double horizontalLength = Math.Sqrt(panel.Normal.X * panel.Normal.X + panel.Normal.Y * panel.Normal.Y);
        if (!double.IsFinite(normalLength) || normalLength <= 0 ||
            !double.IsFinite(horizontalLength) || horizontalLength <= 0)
            return Fail(panel, "Cannot measure facade dimensions from this normal.");
        var normal = new PanelPoint3(panel.Normal.X / normalLength, panel.Normal.Y / normalLength, panel.Normal.Z / normalLength);
        var right = new PanelPoint3(-panel.Normal.Y / horizontalLength, panel.Normal.X / horizontalLength, 0);
        var up = new PanelPoint3(-normal.Z * right.Y, normal.Z * right.X, normal.X * right.Y - normal.Y * right.X);
        var origin = panel.Bounds[0];
        var relative = panel.Bounds.Select(p => new PanelPoint3(p.X - origin.X, p.Y - origin.Y, p.Z - origin.Z)).ToArray();
        double Extent(PanelPoint3 axis) => relative.Max(p => Dot(p, axis)) - relative.Min(p => Dot(p, axis));
        double width = Extent(right), height = Extent(up);
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            return Fail(panel, "Panel dimensions must be finite and positive.");

        string unitWidth = PanelCladdingKeyService.FormatUnitDimension(width);
        string unitHeight = PanelCladdingKeyService.FormatUnitDimension(height);
        var writes = new Dictionary<string, string>(assignment.UserTextWrites, StringComparer.Ordinal)
        {
            [PanelCladdingKeyService.UnitWidthKey] = unitWidth,
            [PanelCladdingKeyService.UnitHeightKey] = unitHeight,
            [PanelCladdingKeyService.UnitDimensionKey] = unitWidth + "x" + unitHeight
        };
        foreach (string key in RequiredKeys)
        {
            // A key that already exists (any case, blank or not) is never overwritten; many of
            // these fields are hand-filled. Only a missing key gets the placeholder.
            if (writes.ContainsKey(key) ||
                panel.UserText.Keys.Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase))) continue;
            // Empty user strings are not retained reliably by Rhino. A space is the
            // same persisted blank convention used by the editor's unassigned cells.
            writes[key] = PanelCladdingKeyService.PersistedBlankCellValue;
        }
        ReserveRoleDefinition(writes, panel.UserText);
        // Existing keys are not copied into the writes, so the CID check reads both.
        var effective = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string value) in panel.UserText) effective[key] = value;
        foreach ((string key, string value) in writes) effective[key] = value;
        if (assignment.Cid != PanelCladdingCidService.FromPanelId(assignment.Pid) + PanelCladdingCidService.RoleSuffix(effective))
            return Fail(panel, "Unit-type case variants disagree with the planned CID; consolidate CW_1.06_UNIT_TYPE before running PCpid.");
        return OperationResponse<IReadOnlyDictionary<string, string>>.Ok(writes);
    }

    // Definition A is reserved for the panel role. A non-role value moves the existing
    // definitions down one letter (adding E, F, ... as needed); flat and curved panels are
    // always parent, multi-face panels keep their role. Roles are stored lowercase.
    public static void ReserveRoleDefinition(IDictionary<string, string> writes, IReadOnlyDictionary<string, string> userText)
    {
        string Value(string key) => writes.TryGetValue(key, out string? written) ? written
            : userText.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
        int count = Enumerable.Range(0, 26)
            .Select(index => DefinitionKey(index))
            .TakeWhile((key, index) => index < 4 || writes.ContainsKey(key) ||
                userText.Keys.Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
            .Count();
        var values = Enumerable.Range(0, count).Select(index => Value(DefinitionKey(index))).ToList();
        if (!string.IsNullOrWhiteSpace(values[0]) && !PanelCladdingCidService.IsRole(values[0]))
        {
            values.Insert(0, PanelCladdingKeyService.PersistedBlankCellValue);
        }
        string unitType = Value(PanelCladdingCidService.UnitTypeUserTextKey).Trim().ToUpperInvariant();
        if (unitType is PanelCladdingCidService.FlatUnitType or PanelCladdingCidService.CurvedUnitType)
        {
            values[0] = PanelCladdingCidService.ParentRole;
        }
        else if (PanelCladdingCidService.IsRole(values[0]))
        {
            values[0] = PanelCladdingCidService.NormalizeRole(values[0]);
        }
        for (int index = 0; index < values.Count; index++)
        {
            string key = DefinitionKey(index);
            string value = string.IsNullOrWhiteSpace(values[index])
                ? PanelCladdingKeyService.PersistedBlankCellValue
                : values[index];
            // An existing key that already holds the value is left as it is.
            if (!writes.ContainsKey(key) &&
                userText.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase) && item.Value == value)) continue;
            writes[key] = value;
        }
    }

    private static string DefinitionKey(int index) => "CW_5.00_DEFINITION_" + (char)('A' + index);

    private static bool Finite(PanelPoint3 p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
    private static double Dot(PanelPoint3 a, PanelPoint3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static OperationResponse<IReadOnlyDictionary<string, string>> Fail(PanelCladdingPidPanel panel, string message) =>
        OperationResponse<IReadOnlyDictionary<string, string>>.Fail($"PCpid: panel {panel.ObjectId}: {message}");
}
