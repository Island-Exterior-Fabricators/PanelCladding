using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PcBakeCommandSmoke;

// PCbake (1.3.0): Scope option (Surfaces by default, Curves, All) and existing curves never overwritten.
internal static class Program
{
    private static readonly Guid PanelA = Guid.Parse("10000000-0000-0000-0000-00000000000a");
    private static readonly Guid PanelB = Guid.Parse("10000000-0000-0000-0000-00000000000b");
    private static readonly Guid SurfaceA = Guid.Parse("20000000-0000-0000-0000-00000000000a");
    private static readonly Guid SurfaceStaleA = Guid.Parse("20000000-0000-0000-0000-0000000000a2");
    private static readonly Guid CurveA = Guid.Parse("30000000-0000-0000-0000-00000000000a");
    private static readonly Guid CurveStaleA = Guid.Parse("30000000-0000-0000-0000-0000000000a2");

    private static int Main()
    {
        try
        {
            VerifyScopeTable();
            VerifySurfaceScope();
            VerifyCurveScope();
            VerifyAllScopeKeepsExistingCurves();
            VerifyCommandContract();
            VerifyLiveContract();
            Console.WriteLine("PASS: PCbake command regression");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PCbake smoke failed: {ex}");
            return 1;
        }
    }

    private static void VerifyScopeTable()
    {
        Require(Enum.GetValues<PanelCladdingBakeScope>().First() == PanelCladdingBakeScope.Surfaces,
            "Surfaces must be the first (default) scope.");
        Require(Includes(PanelCladdingBakeScope.Surfaces, PanelCladdingDependencyKind.Surface) &&
                !Includes(PanelCladdingBakeScope.Surfaces, PanelCladdingDependencyKind.Curve) &&
                !Includes(PanelCladdingBakeScope.Curves, PanelCladdingDependencyKind.Surface) &&
                Includes(PanelCladdingBakeScope.Curves, PanelCladdingDependencyKind.Curve) &&
                Includes(PanelCladdingBakeScope.All, PanelCladdingDependencyKind.Surface) &&
                Includes(PanelCladdingBakeScope.All, PanelCladdingDependencyKind.Curve),
            "Scope inclusion table is wrong.");
        Console.WriteLine("[OK] Surfaces is the default scope; each scope includes exactly its object kinds");
    }

    private static void VerifySurfaceScope()
    {
        PanelCladdingDependencyReconciliationPlan plan = Plan(PanelCladdingBakeScope.Surfaces,
            [
                Expected(PanelA, "CID-A_0A", PanelCladdingDependencyKind.Surface),
                Expected(PanelB, "CID-B_0A", PanelCladdingDependencyKind.Surface),
                Expected(PanelB, "CID-B_FRM-0", PanelCladdingDependencyKind.Curve)
            ],
            [
                Existing(SurfaceA, PanelA, "CID-A_0A", PanelCladdingDependencyKind.Surface),
                Existing(SurfaceStaleA, PanelA, "CID-A_OLD", PanelCladdingDependencyKind.Surface),
                Existing(CurveStaleA, PanelA, "CID-A_OLD", PanelCladdingDependencyKind.Curve)
            ]);
        Require(plan.Creates.Select(item => item.Cid).SequenceEqual(["CID-B_0A"]) &&
                plan.Updates.Select(action => action.ObjectId).SequenceEqual([SurfaceA]) &&
                plan.Deletes.SequenceEqual([SurfaceStaleA]),
            "The Surfaces scope must create, rebuild and delete surfaces only.");
        Require(plan.PreservedCurvePanelIds.Count == 0,
            "The Surfaces scope must not report kept curves.");
        Console.WriteLine("[OK] Surfaces scope reconciles surfaces and leaves every curve alone");
    }

    private static void VerifyCurveScope()
    {
        PanelCladdingDependencyReconciliationPlan plan = Plan(PanelCladdingBakeScope.Curves,
            [
                Expected(PanelB, "CID-B_0A", PanelCladdingDependencyKind.Surface),
                Expected(PanelB, "CID-B_FRM-0", PanelCladdingDependencyKind.Curve),
                Expected(PanelB, "CID-B_INT-A1", PanelCladdingDependencyKind.Curve)
            ],
            [
                Existing(SurfaceStaleA, PanelA, "CID-A_OLD", PanelCladdingDependencyKind.Surface)
            ]);
        Require(plan.Creates.Select(item => item.Cid).OrderBy(cid => cid, StringComparer.Ordinal)
                    .SequenceEqual(["CID-B_FRM-0", "CID-B_INT-A1"]) &&
                plan.Updates.Count == 0 && plan.Deletes.Count == 0,
            "The Curves scope must create the curves of a panel without curves and leave surfaces alone.");
        Console.WriteLine("[OK] Curves scope bakes curves for panels without curves and leaves surfaces alone");
    }

    private static void VerifyAllScopeKeepsExistingCurves()
    {
        PanelCladdingDependencyReconciliationPlan plan = Plan(PanelCladdingBakeScope.All,
            [
                Expected(PanelA, "CID-A_0A", PanelCladdingDependencyKind.Surface),
                Expected(PanelA, "CID-A_FRM-0", PanelCladdingDependencyKind.Curve),
                Expected(PanelA, "CID-A_INT-A1", PanelCladdingDependencyKind.Curve),
                Expected(PanelB, "CID-B_FRM-0", PanelCladdingDependencyKind.Curve)
            ],
            [
                Existing(SurfaceA, PanelA, "CID-A_0A", PanelCladdingDependencyKind.Surface),
                Existing(CurveA, PanelA, "CID-A_FRM-0", PanelCladdingDependencyKind.Curve),
                Existing(CurveStaleA, PanelA, "CID-A_OLD", PanelCladdingDependencyKind.Curve)
            ]);
        Require(plan.Creates.Select(item => item.Cid).SequenceEqual(["CID-B_FRM-0"]),
            "Only the panel without curves may get new curves; a panel with curves gets none, not even missing ones.");
        Require(plan.Updates.Select(action => action.ObjectId).SequenceEqual([SurfaceA]) &&
                plan.Deletes.Count == 0,
            "An existing curve, current or stale, must never be rebuilt or deleted.");
        Require(plan.PreservedCurvePanelIds.SequenceEqual([PanelA]),
            "The panel whose curves were kept must be reported.");
        Require(PanelCladdingDependencyReconciliationService.CurvePreservedPanels(
                    [Existing(CurveA, Guid.Empty, "CID-A_FRM-0", PanelCladdingDependencyKind.Curve)]).Count == 0,
            "A curve without an owning panel cannot mark a panel as kept.");
        Console.WriteLine("[OK] All scope keeps every curve of a panel that has curves and reports it");
    }

    private static void VerifyCommandContract()
    {
        string source = Source("src", "PanelCladdingEditor", "UI", "PanelCladdingBakeCommand.cs");
        string[] required =
        {
            "EnglishName => \"PCbake\"",
            "[Guid(\"F2C41D75-2F88-4CB4-9D1C-CF6BD1D6208A\")]",
            "Enum.GetNames<PanelCladdingBakeScope>()",
            "int scopeIndex = 0;",
            "getter.AddOptionList(\"Scope\", ScopeNames, scopeIndex)",
            "scopeIndex = getter.Option().CurrentListOptionIndex;",
            "getter.ObjectsWerePreselected",
            "(PanelCladdingBakeScope)scopeIndex",
            "bakesSurfaces\n            ? ResolveMaterialColors(doc)",
            "selection.Scope);"
        };
        string normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (string token in required)
        {
            Require(normalized.Contains(token, StringComparison.Ordinal), $"PCbake command is missing: {token}");
        }
        Require(!normalized.Contains("static int scopeIndex", StringComparison.Ordinal) &&
                !normalized.Contains("static PanelCladdingBakeScope", StringComparison.Ordinal),
            "The scope must reset to Surfaces on every run, not persist in a static field.");
        foreach (string removed in new[] { "PanelCladdingSpawnSrfCommand.cs", "PanelCladdingSpawnCrvCommand.cs", "PanelCladdingUpdateCommand.cs" })
        {
            Require(!File.Exists(Path.Combine(RegressionPaths.RepositoryRoot, "src", "PanelCladdingEditor", "UI", removed)),
                $"{removed} must be removed.");
        }
        Console.WriteLine("[OK] PCbake offers Scope from Surfaces each run, also after preselection; spawn/update commands are gone");
    }

    private static void VerifyLiveContract()
    {
        string source = Source("src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live", "PanelCladding",
            "LivePanelCladdingUpdateService.cs").Replace("\r\n", "\n", StringComparison.Ordinal);
        int read = source.IndexOf("IReadOnlyList<PanelCladdingExistingDependency> existing = ReadManagedDependencies(", StringComparison.Ordinal);
        int prepare = source.IndexOf("preparedResponse = PrepareDependencies(", StringComparison.Ordinal);
        Require(read >= 0 && prepare > read,
            "Existing dependencies must be read before generation so kept curves need no curve plan.");
        string[] required =
        {
            "PanelCladdingDependencyReconciliationService.CurvePreservedPanels(existing)",
            "!curvePreservedPanels.Contains(source.ObjectId)",
            "SourcePanelObjectId = owners[0].ObjectId,",
            "PreservedCurvePanelIds = plan.PreservedCurvePanelIds,",
            "existing,\n                    scope);"
        };
        foreach (string token in required)
        {
            Require(source.Contains(token, StringComparison.Ordinal), $"Live PCbake is missing: {token}");
        }
        Console.WriteLine("[OK] live bake reads existing dependencies first and skips curve planning for kept panels");
    }

    private static bool Includes(PanelCladdingBakeScope scope, PanelCladdingDependencyKind kind) =>
        PanelCladdingDependencyReconciliationService.Includes(scope, kind);

    private static PanelCladdingDependencyReconciliationPlan Plan(
        PanelCladdingBakeScope scope,
        IReadOnlyList<PanelCladdingExpectedDependency> expected,
        IReadOnlyList<PanelCladdingExistingDependency> existing)
    {
        OperationResponse<PanelCladdingDependencyReconciliationPlan> plan =
            new PanelCladdingDependencyReconciliationService().CreatePlan(expected, existing, scope);
        if (!plan.Success || plan.Data is null)
        {
            throw new InvalidOperationException($"{scope} plan failed: {plan.Message}");
        }
        return plan.Data;
    }

    private static PanelCladdingExpectedDependency Expected(Guid panel, string cid, PanelCladdingDependencyKind kind) => new()
    {
        SourcePanelObjectId = panel,
        PanelId = panel == PanelA ? "PID-A" : "PID-B",
        Cid = cid,
        Kind = kind
    };

    private static PanelCladdingExistingDependency Existing(
        Guid objectId, Guid panel, string cid, PanelCladdingDependencyKind kind) => new()
    {
        ObjectId = objectId,
        SourcePanelObjectId = panel,
        PanelId = panel == PanelB ? "PID-B" : "PID-A",
        Cid = cid,
        Kind = kind
    };

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine([RegressionPaths.RepositoryRoot, .. parts]));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
