using System.Globalization;
using System.Text.RegularExpressions;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public static partial class PanelCladdingCidService
{
    // Unit types: C Curved, F Flat, L Two Face, Z Three Face. Multi-face roles live in
    // CW_5.00_DEFINITION_A as Parent / Child / Child 1 / Child 2 (flat and curved are Parent).
    public const string UnitTypeUserTextKey = "CW_1.06_UNIT_TYPE";
    public const string RoleDefinitionKey = "CW_5.00_DEFINITION_A";
    public const string CurvedUnitType = "C";
    public const string FlatUnitType = "F";
    public const string TwoFaceUnitType = "L";
    public const string ThreeFaceUnitType = "Z";
    public const string ParentRole = "Parent";
    public const string ChildRole = "Child";
    // '-' separates the tokens of one identifier (PID-ABC-N2-05-05, CID-ABC-N2-05-05);
    // '_' joins a panel CID to its role and component codes (CID-ABC-N2-05-05_C_0A).
    public const string IdSeparator = "-";
    public const string ComponentSeparator = "_";
    public const string PanelIdPrefix = "PID" + IdSeparator;
    public const string CidPrefix = "CID" + IdSeparator;
    private const string ProjectCidPrefix = CidPrefix + "ABC" + IdSeparator;
    private const string ParentRoleSuffix = ComponentSeparator + "P";
    private const string ChildRoleSuffix = ComponentSeparator + "C";

    public static bool IsMultiFaceUnitType(string? unitType) =>
        string.Equals(unitType?.Trim(), TwoFaceUnitType, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(unitType?.Trim(), ThreeFaceUnitType, StringComparison.OrdinalIgnoreCase);

    public static bool IsRole(string? value) => RoleRegex().IsMatch(value?.Trim() ?? string.Empty);

    public static string ShortName(string cid)
    {
        string value = (cid ?? string.Empty).Trim();
        int prefixLength = value.StartsWith(ProjectCidPrefix, StringComparison.OrdinalIgnoreCase) ? ProjectCidPrefix.Length :
            value.StartsWith(CidPrefix, StringComparison.OrdinalIgnoreCase) ? CidPrefix.Length : 0;
        return value.Length > prefixLength ? value[prefixLength..] : value;
    }

    public static string Component(string cid, string component) =>
        $"{cid}{ComponentSeparator}{(component ?? string.Empty).Trim()}";

    public static string? PanelNameWrite(IReadOnlyDictionary<string, string> userText)
    {
        string cid = PanelCidWrite(userText) ??
            Get(userText, PanelCladdingSpawnPlanningService.CidUserTextKey).Trim();
        return cid.Length > 0 ? ShortName(cid) : null;
    }

    public static string FromPanelId(string panelId)
    {
        string pid = (panelId ?? string.Empty).Trim();
        return pid.StartsWith(PanelIdPrefix, StringComparison.OrdinalIgnoreCase)
            ? CidPrefix + pid[PanelIdPrefix.Length..]
            : pid;
    }

    public static string RoleSuffix(IReadOnlyDictionary<string, string> userText) =>
        UnitTypeSuffix(userText) ?? string.Empty;

    // Null means unspecified/unsupported; an empty suffix is an explicit single-face panel.
    // Two- and three-face panels take _P, _C or _C<n> from their Definition A role.
    private static string? UnitTypeSuffix(IReadOnlyDictionary<string, string> userText)
    {
        string unitType = Get(userText, UnitTypeUserTextKey).Trim().ToUpperInvariant();
        if (unitType is CurvedUnitType or FlatUnitType)
        {
            return string.Empty;
        }
        if (!IsMultiFaceUnitType(unitType))
        {
            return null;
        }
        Match role = RoleRegex().Match(Get(userText, RoleDefinitionKey).Trim());
        if (!role.Success)
        {
            return null;
        }
        return role.Groups["parent"].Success
            ? ParentRoleSuffix
            : ChildRoleSuffix + (role.Groups["index"].Success
                ? int.Parse(role.Groups["index"].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
                : string.Empty);
    }

    public static string ResolvePanelCid(
        string panelId,
        IReadOnlyDictionary<string, string> userText,
        string? existingCid = null)
    {
        string canonical = FromPanelId(panelId);
        string? suffix = UnitTypeSuffix(userText);
        string stored = (existingCid ?? Get(userText, PanelCladdingSpawnPlanningService.CidUserTextKey)).Trim();
        if (suffix is not null)
        {
            return canonical + suffix;
        }
        return stored.Length == 0 ? canonical : stored;
    }

    public static string SurfaceCid(
        string panelId,
        string cellLabel,
        IReadOnlyDictionary<string, string> userText) =>
        Component(FromPanelId(panelId) + RoleSuffix(userText), cellLabel);

    // Explicit unit types own the canonical CID, including removing stale corner suffixes
    // for flat panels. Missing/unsupported unit types and missing PIDs are not rewritten.
    public static string? PanelCidWrite(IReadOnlyDictionary<string, string> userText)
    {
        string pid = Get(userText, PanelCladdingSpawnPlanningService.PanelIdUserTextKey).Trim();
        if (pid.Length == 0 || UnitTypeSuffix(userText) is null)
        {
            return null;
        }
        return ResolvePanelCid(pid, userText);
    }

    public static void AddPanelCidWrite(
        IDictionary<string, string> writes,
        IReadOnlyDictionary<string, string> userText)
    {
        if (PanelCidWrite(userText) is string cid)
        {
            writes[PanelCladdingSpawnPlanningService.CidUserTextKey] = cid;
        }
    }

    public static bool IncludesDependency(string panelId, string panelCid, string dependencyCid)
    {
        string prefix = FromPanelId(panelId) + ComponentSeparator;
        if (dependencyCid.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            Match role = RoleComponentRegex().Match(dependencyCid[prefix.Length..]);
            if (role.Success)
            {
                return string.Equals(panelCid, prefix + role.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
            }
        }
        return true; // Existing unsuffixed dependencies can be migrated by PCUpdate.
    }

    private static string Get(IReadOnlyDictionary<string, string> userText, string key) =>
        userText.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            .Value ?? string.Empty;

    [GeneratedRegex(@"^(?:(?<parent>parent)|child(?:\s*(?<index>[0-9]+))?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RoleRegex();

    // The role component of a dependency CID: P_, C_ or C<n>_ before the cell/curve code.
    [GeneratedRegex(@"^(P|C[0-9]*)_", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RoleComponentRegex();
}
