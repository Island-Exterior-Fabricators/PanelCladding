using System.Text.RegularExpressions;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingIdSeparatorsSmoke;

internal static class Program
{
    private static void Main()
    {
        VerifyPanelIds();
        VerifyComponentIds();
        VerifyCurveCodes();
        VerifyFrameAssignmentKeys();
        VerifyNoRetiredSeparatorsInSource();
        Console.WriteLine("PASS: panel cladding PID/CID separator regression");
    }

    private static void VerifyPanelIds()
    {
        // The user's reference example: CID_ABC_N2_05_05-C becomes CID-ABC-N2-05-05_C.
        Require(PanelCladdingCidService.ResolvePanelCid("PID-ABC-N2-05-05", Text("L", "Child")) == "CID-ABC-N2-05-05_C",
            "Corner child CID must be CID-ABC-N2-05-05_C.");
        Require(PanelCladdingCidService.ResolvePanelCid("PID-ABC-N2-05-05", Text("L", "Parent")) == "CID-ABC-N2-05-05_P" &&
                PanelCladdingCidService.ResolvePanelCid("PID-ABC-N2-05-05", Text("F", "Parent", "CID-ABC-N2-05-05_P")) == "CID-ABC-N2-05-05",
            "Parent role uses _P and flat panels drop a stale role.");
        Require(PanelCladdingCidService.FromPanelId(" PID-ABC-E1-PF-07 ") == "CID-ABC-E1-PF-07" &&
                PanelCladdingCidService.FromPanelId("PID_ABC_E1_05_45") == "PID_ABC_E1_05_45",
            "Only the PID- prefix maps to CID-; retired PID_ values are not reinterpreted.");

        var plan = new PanelCladdingPidPlanningService().CreatePlan(
            new PanelCladdingPidRequest("abc", [PanelId], PanelId, PanelId),
            [new PanelCladdingPidPanel(PanelId, new PanelPoint3(0, -1, 0),
                [new(0, 0, 0), new(30, 0, 0), new(30, 0, 20), new(0, 0, 20)], Text("L", "Child"))],
            0.00001, Math.PI / 180);
        Require(plan.Success && plan.Data is not null, plan.Message);
        Require(plan.Data!.Panels.Single().Pid == "PID-ABC-N1-01-01" && plan.Data.Panels.Single().Cid == "CID-ABC-N1-01-01_C",
            "PCpid must write PID-ABC-N1-01-01 / CID-ABC-N1-01-01_C.");

        (string Cid, string Name)[] names =
        {
            ("CID-ABC-N2-05-05_C", "N2-05-05_C"), ("CID-ABC-N2-05-05_C_0A", "N2-05-05_C_0A"),
            ("CID-ABC-E1-04-01_FRM-0", "E1-04-01_FRM-0"), ("CID-QRS-N1-01-01", "N1-01-01"), ("CUSTOM", "CUSTOM")
        };
        foreach ((string cid, string name) in names)
        {
            Require(PanelCladdingCidService.ShortName(cid) == name, $"ShortName({cid}) must be {name}.");
        }
        Console.WriteLine("[OK] PIDs and panel CIDs use '-' inside the ID and '_' before the role");
    }

    private static void VerifyComponentIds()
    {
        IReadOnlyDictionary<string, string> parent = Text("L", "Parent");
        Require(PanelCladdingCidService.SurfaceCid("PID-ABC-W1-03-01", "0A", parent) == "CID-ABC-W1-03-01_P_0A" &&
                PanelCladdingSpawnPlanningService.BuildSurfaceCid("PID-ABC-W1-03-01", "1B") == "CID-ABC-W1-03-01_1B",
            "Surface CIDs join cell labels with '_'.");
        Require(PanelCladdingCidService.IncludesDependency("PID-ABC-W1-03-01", "CID-ABC-W1-03-01_P", "CID-ABC-W1-03-01_P_0A") &&
                !PanelCladdingCidService.IncludesDependency("PID-ABC-W1-03-01", "CID-ABC-W1-03-01_P", "CID-ABC-W1-03-01_C_INT-B1") &&
                PanelCladdingCidService.IncludesDependency("PID-ABC-W1-03-01", "CID-ABC-W1-03-01_C", "CID-ABC-W1-03-01_FRM-0"),
            "Role ownership is decided by the _P_/_C_ component prefix.");
        Console.WriteLine("[OK] surface and curve CIDs append components with '_'");
    }

    private static void VerifyCurveCodes()
    {
        var keySet = new PanelCladdingKeySet
        {
            HorizontalOffsets = [30d],
            VerticalOffsets = [40d],
            Cells = Array.Empty<PanelCladdingCell>(),
            Topology = new PanelCladdingTopologyState
            {
                MergeRuns = [new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Horizontal, 0, 0, 1)]
            }
        };
        IReadOnlyList<PanelCladdingExtrusionCurvePlan> curves = Required(new PanelCladdingExtrusionPlanningService().CreatePlan(
            "PID-ABC-N2-05-05", "CID-ABC-N2-05-05_C", 100d, 80d, keySet,
            "ABC-IEF-Wireframe::Surfaces - PNL::Surfaces - WT_01"));
        string[] codes = curves.Select(curve => curve.Code).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        Require(codes.SequenceEqual(new[] { "FRM-0", "FRM-1", "FRM-2", "FRM-3", "INT-0A", "INT-0B", "INT-A" }),
            "Curve codes must use '-': " + string.Join(", ", codes));
        Require(curves.All(curve => curve.Cid == "CID-ABC-N2-05-05_C_" + curve.Code &&
                                    curve.UserTextWrites["CRV"] == curve.Code &&
                                    curve.UserTextWrites["CW_1.01_PID"] == "PID-ABC-N2-05-05"),
            "Curve CID and CRV must be CID-ABC-N2-05-05_C_FRM-0 style.");

        var partial = new PanelCladdingKeySet
        {
            HorizontalOffsets = [30d],
            VerticalOffsets = [20d, 40d],
            Cells = Array.Empty<PanelCladdingCell>(),
            Topology = new PanelCladdingTopologyState
            {
                MergeRuns = [new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Horizontal, 0, 0, 1)]
            }
        };
        Require(Required(new PanelCladdingExtrusionPlanningService().CreatePlan(
                    "PID-T-1", "CID-T-1", 100d, 80d, partial, "T-IEF-Wireframe::Surfaces - PNL::Surfaces - WT_01"))
                .Any(curve => curve.Code == "INT-A0–1"),
            "Partial merged runs keep the en dash range: INT-A0–1.");
        Console.WriteLine("[OK] CRV codes are FRM-n / INT-xy and curve CIDs end in _<code>");
    }

    private static void VerifyFrameAssignmentKeys()
    {
        var service = new PanelFrameAssignmentService();
        var topology = new PanelCladdingTopologyState();
        OperationResponse<PanelFrameAssignmentState> accepted = service.Decode(
            "{\"v\":2,\"f\":{\"FRM-2\":[\"1D-ALU-H0651\"]},\"s\":[],\"d\":{},\"x\":{\"FRM-2\":4}}", 0, 0, topology);
        Require(accepted.Success && accepted.Data!.FrameAssignments.ContainsKey("FRM-2") &&
                accepted.Data.CurveModifiers["FRM-2"] == 4d,
            "CW_1.09_FRAME_TYPE perimeter keys and modifiers use FRM-n: " + accepted.Message);
        OperationResponse<PanelFrameAssignmentState> retired = service.Decode(
            "{\"v\":2,\"f\":{\"FRM_2\":[\"1D-ALU-H0651\"]},\"s\":[],\"d\":{}}", 0, 0, topology);
        Require(!retired.Success && retired.Message.Contains("FRAME_INVALID", StringComparison.Ordinal),
            "Retired FRM_n perimeter keys fail validation.");
        Console.WriteLine("[OK] saved profile assignments key perimeter frames as FRM-n");
    }

    private static void VerifyNoRetiredSeparatorsInSource()
    {
        string sourceRoot = Path.Combine(FindRepositoryRoot(), "src", "PanelCladdingEditor");
        var retired = new Regex("\"(?:PID_|CID_|FRM_|INT_)|[\"$]INT_\\{|\"-[PC]\"", RegexOptions.CultureInvariant);
        string[] hits = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "bin-testhost" or "obj-testhost"))
            .SelectMany(path => File.ReadAllLines(path).Select((line, index) => (path, line, index)))
            .Where(item => retired.IsMatch(item.line))
            .Select(item => $"{Path.GetFileName(item.path)}:{item.index + 1}")
            .ToArray();
        Require(hits.Length == 0, "Retired ID or curve-code literals remain: " + string.Join(", ", hits));
        Console.WriteLine("[OK] plug-in source has no PID_/CID_/FRM_/INT_ or -P/-C literals");
    }

    private static readonly Guid PanelId = Guid.Parse("00000000-0000-0000-0000-000000000123");

    private static IReadOnlyDictionary<string, string> Text(string unitType, string role, string? cid = null)
    {
        var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingCidService.UnitTypeUserTextKey] = unitType,
            [PanelCladdingCidService.RoleDefinitionKey] = role
        };
        if (cid is not null)
        {
            text[PanelCladdingSpawnPlanningService.CidUserTextKey] = cid;
        }
        return text;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Repository root containing AGENTS.md was not found.");
    }

    private static T Required<T>(OperationResponse<T> response)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException(response.Message);
        }
        return response.Data;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
