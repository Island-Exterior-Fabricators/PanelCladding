using System.Globalization;
using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

var setup = new PanelCladdingPidSetupService();
var planner = new PanelCladdingPidPlanningService();
string[] expectedKeys = [
    "CW_1.00_PNL", "CW_1.01_PID", "CW_1.02_CID", "CW_1.03_ELEVATION", "CW_1.04_LEVEL",
    "CW_1.05_LOT", "CW_1.06_UNIT_TYPE", "CW_1.07_WALL_TYPE", "CW_1.08_FRAME_CONFIG",
    "CW_1.09_FRAME_TYPE", "CW_1.10_CAD_TYPE", "CW_1.11_PHASE", "CW_2.00_UNIT_DIMENSION",
    "CW_2.01_UNIT_WIDTH", "CW_2.02_UNIT_HEIGHT", "CW_2.05_ANCHOR_DL_LEFT", "CW_2.05_ANCHOR_DL_RIGHT",
    "CW_2.05_ANCHOR_WL_LEFT", "CW_2.05_ANCHOR_WL_RIGHT", "CW_2.06_PENETRATION_DIMENSION",
    "CW_5.00_DEFINITION_A", "CW_5.00_DEFINITION_B", "CW_5.00_DEFINITION_C", "CW_5.00_DEFINITION_D",
    "CW_6.00_WL_NEGATIVE", "CW_6.00_WL_POSITIVE", "CW_6.01_PSF", "CW_6.02_UNIT_WEIGHT",
    "CW_7.00_BLIND_SINGLE", "CW_7.00_BLIND_DOUBLE"
];
var panel = Rect(0, 0, 123.456789, 67.891234);
var assignment = Plan(panel);
var blankWrites = Required(setup.CreateWrites(assignment, panel));
Check(blankWrites.Count == 30 && blankWrites.Keys.ToHashSet().SetEquals(expectedKeys), "All 30 exact user-requested keys");
Check(blankWrites["CW_2.01_UNIT_WIDTH"] == "123.45679" && blankWrites["CW_2.02_UNIT_HEIGHT"] == "67.89123" &&
    blankWrites["CW_2.00_UNIT_DIMENSION"] == "123.45679x67.89123", "Generated dimensions and established precision");
Check(assignment.UserTextWrites.All(p => blankWrites[p.Key] == p.Value), "Generated IDs remain coherent");
Check(blankWrites.Count(p => p.Value == " ") == 23 && blankWrites.All(p => p.Value.Length > 0), "Every unassigned field has a retained blank");
Console.WriteLine("[OK] all 30 keys, 23 persisted blanks and seven generated values");

var text = expectedKeys.ToDictionary(k => k.ToLowerInvariant(), k => "preserve:" + k);
text["cw_1.06_unit_type"] = " l ";
text["cw_5.00_definition_a"] = "Child";
text["cw_1.05_lot"] = " Lot 12 ";
text["CW_1.05_LOT"] = "";
text["unrelated"] = "keep";
panel = panel with { UserText = text };
string before = JsonSerializer.Serialize(panel);
assignment = Plan(panel);
var writes = Required(setup.CreateWrites(assignment, panel));
string[] generated = ["CW_1.01_PID", "CW_1.02_CID", "CW_1.03_ELEVATION", "CW_1.04_LEVEL",
    "CW_2.00_UNIT_DIMENSION", "CW_2.01_UNIT_WIDTH", "CW_2.02_UNIT_HEIGHT"];
Check(expectedKeys.Except(generated).All(k => writes[k] == text[k.ToLowerInvariant()]), "Existing non-generated values preserved byte-for-byte");
Check(writes["CW_1.02_CID"] == "CID-ABC-N1-01-01_C", "CID agrees with retained unit role");
Check(!writes.ContainsKey("unrelated") && before == JsonSerializer.Serialize(panel), "Unrelated text is outside writes and source is immutable");
var applied = Apply(text, writes);
Check(expectedKeys.All(applied.ContainsKey) && applied.Count == 31 && applied["unrelated"] == "keep", "Canonical merge retains all keys plus unrelated text");
var repeat = panel with { UserText = applied };
var repeatWrites = Required(setup.CreateWrites(Plan(repeat), repeat));
Check(Same(applied, Apply(applied, repeatWrites)), "Repeat is idempotent");
var conflict = panel with { UserText = new Dictionary<string, string>(text) { ["CW_1.05_LOT"] = "different" } };
Check(!setup.CreateWrites(Plan(conflict), conflict).Success, "Conflicting case variants rejected");
var ambiguousRole = panel with { UserText = new Dictionary<string, string>
    { ["CW_1.06_UNIT_TYPE"] = "", ["cw_1.06_unit_type"] = "L", ["CW_5.00_DEFINITION_A"] = "Parent" } };
Check(!setup.CreateWrites(Plan(ambiguousRole), ambiguousRole).Success, "Canonicalized role cannot silently disagree with CID");
var flat = panel with { UserText = new Dictionary<string, string>(text) { ["cw_1.06_unit_type"] = "F", ["cw_5.00_definition_a"] = "Glass note" } };
var flatWrites = Required(setup.CreateWrites(Plan(flat), flat));
Check(flatWrites["CW_5.00_DEFINITION_A"] == "Parent" && flatWrites["CW_5.00_DEFINITION_B"] == "Glass note" &&
    flatWrites["CW_5.00_DEFINITION_C"] == text["cw_5.00_definition_b"] &&
    flatWrites["CW_5.00_DEFINITION_E"] == text["cw_5.00_definition_d"] && flatWrites["CW_1.02_CID"] == "CID-ABC-N1-01-01",
    "A flat panel is Parent; a taken Definition A moves the definitions down to E");
var formerChild = panel with { UserText = new Dictionary<string, string>(text) { ["cw_1.06_unit_type"] = "F" } };
var formerChildWrites = Required(setup.CreateWrites(Plan(formerChild), formerChild));
Check(formerChildWrites["CW_5.00_DEFINITION_A"] == "Parent" && !formerChildWrites.ContainsKey("CW_5.00_DEFINITION_E"),
    "A panel changed to flat becomes Parent");
Console.WriteLine("[OK] existing values, role CID, Definition A reservation, canonical casing, repeat stability and conflict preflight");

CultureInfo previous = CultureInfo.CurrentCulture;
try
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
    foreach (double angle in new[] { 0, Math.PI / 2, Math.PI, 3 * Math.PI / 2, .713 })
    foreach (double tilt in new[] { 0, .005 })
    {
        var rotated = Rect(angle, tilt, 123.456789, 67.891234);
        var translated = rotated with { Bounds = rotated.Bounds.Select(p => new PanelPoint3(p.X + 1e7, p.Y - 2e7, p.Z + 1e6)).ToArray() };
        var rotatedWrites = Required(setup.CreateWrites(Plan(translated), translated));
        Check(rotatedWrites["CW_2.00_UNIT_DIMENSION"] == "123.45679x67.89123", "Panel-plane extents survive orientation, permitted tilt, translation and culture");
    }
}
finally { CultureInfo.CurrentCulture = previous; }
var scaled = Rect(.713, 0, 1234.56789, 678.91234);
Check(Required(setup.CreateWrites(Plan(scaled), scaled))["CW_2.00_UNIT_DIMENSION"] == "1234.56789x678.91234", "Dimensions remain in document model units");
Check(!setup.CreateWrites(assignment with { ObjectId = Guid.NewGuid() }, panel).Success, "Mismatched source rejected");
Check(!setup.CreateWrites(assignment, panel with { Bounds = [] }).Success, "Missing dimensions rejected");
Check(!setup.CreateWrites(assignment, panel with { Normal = new(0, 0, 1) }).Success, "Horizontal input rejected");
Console.WriteLine("[OK] cardinal/rotated/tilted planes, translation, culture, units and invalid dimensions");

var lower = Rect(0, 0, 30, 20);
var upper = lower with { ObjectId = Guid.NewGuid(), Bounds = lower.Bounds.Select(p => p with { Z = p.Z + 20 }).ToArray() };
var context = new[] { lower, upper };
before = JsonSerializer.Serialize(context);
var partial = Required(planner.CreatePlan(new("ABC", [upper.ObjectId], lower.ObjectId, lower.ObjectId), context, .00001, Math.PI / 180));
Check(partial.Panels.Count == 1 && partial.Panels[0].ObjectId == upper.ObjectId, "Only selected panel enters mutation preparation");
writes = Required(setup.CreateWrites(partial.Panels[0], upper));
Check(writes.Count == 30 && writes["CW_1.01_PID"] == "PID-ABC-N1-02-01" && writes["CW_2.00_UNIT_DIMENSION"] == "30.00000x20.00000", "Setup uses full-context numbering with selected dimensions");
Check(JsonSerializer.Serialize(context) == before, "Selected and unselected snapshots are unchanged");
Console.WriteLine("PASS: PCpid setup metadata and dimensions");

PanelCladdingPidAssignment Plan(PanelCladdingPidPanel source) => Required(planner.CreatePlan(
    new("ABC", [source.ObjectId], source.ObjectId, source.ObjectId), [source], .00001, Math.PI / 180)).Panels.Single();
static PanelCladdingPidPanel Rect(double angle, double tilt, double width, double height)
{
    var normal = new PanelPoint3(Math.Sin(angle) * Math.Cos(tilt), Math.Cos(angle) * Math.Cos(tilt), Math.Sin(tilt));
    var right = new PanelPoint3(-Math.Cos(angle), Math.Sin(angle), 0);
    var up = new PanelPoint3(-normal.Z * right.Y, normal.Z * right.X, Math.Cos(tilt));
    PanelPoint3 Point(double x, double z) => new(right.X * x + up.X * z, right.Y * x + up.Y * z, up.Z * z);
    return new(Guid.NewGuid(), normal, [Point(0, 0), Point(width, 0), Point(width, height), Point(0, height)], new Dictionary<string, string>());
}
static Dictionary<string, string> Apply(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> writes)
{
    var result = before.Where(p => !writes.Keys.Contains(p.Key, StringComparer.OrdinalIgnoreCase)).ToDictionary(p => p.Key, p => p.Value);
    foreach (var write in writes) result.Add(write.Key, write.Value);
    return result;
}
static bool Same(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var value) && value == p.Value);
static T Required<T>(OperationResponse<T> response) => response.Success && response.Data is not null ? response.Data : throw new InvalidOperationException(response.Message);
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
