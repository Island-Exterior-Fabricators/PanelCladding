using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PcBakeCommandSmoke;

// PCbake: Scope option (Surfaces by default, Curves, All); surfaces and curves alike are overwritten
// from the panel's current values, created when missing and deleted when obsolete or duplicate.
// A panel with its own problem is skipped whole and marked with a dot; the other panels still bake.
internal static class Program
{
    private static readonly Guid PanelA = Guid.Parse("10000000-0000-0000-0000-00000000000a");
    private static readonly Guid PanelB = Guid.Parse("10000000-0000-0000-0000-00000000000b");
    private static readonly Guid SurfaceA = Guid.Parse("20000000-0000-0000-0000-00000000000a");
    private static readonly Guid SurfaceStaleA = Guid.Parse("20000000-0000-0000-0000-0000000000a2");
    private static readonly Guid CurveA = Guid.Parse("30000000-0000-0000-0000-00000000000a");
    private static readonly Guid CurveStaleA = Guid.Parse("30000000-0000-0000-0000-0000000000a2");
    private static readonly Guid CurveDuplicateA = Guid.Parse("30000000-0000-0000-0000-0000000000a3");

    private static int Main()
    {
        try
        {
            VerifyScopeTable();
            VerifySurfaceScope();
            VerifyCurveScope();
            VerifyCurveScopeOverwritesExistingCurves();
            VerifyAllScopeOverwritesExistingCurves();
            VerifyCommandContract();
            VerifyLiveContract();
            VerifyIssueDotRules();
            VerifyPerPanelContract();
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

    private static void VerifyCurveScopeOverwritesExistingCurves()
    {
        PanelCladdingDependencyReconciliationPlan plan = Plan(PanelCladdingBakeScope.Curves,
            [
                Expected(PanelA, "CID-A_0A", PanelCladdingDependencyKind.Surface),
                Expected(PanelA, "CID-A_FRM-0", PanelCladdingDependencyKind.Curve),
                Expected(PanelA, "CID-A_INT-A1", PanelCladdingDependencyKind.Curve)
            ],
            [
                Existing(SurfaceStaleA, PanelA, "CID-A_OLD", PanelCladdingDependencyKind.Surface),
                Existing(CurveA, PanelA, "CID-A_FRM-0", PanelCladdingDependencyKind.Curve),
                Existing(CurveDuplicateA, PanelA, "CID-A_FRM-0", PanelCladdingDependencyKind.Curve),
                Existing(CurveStaleA, PanelA, "CID-A_OLD", PanelCladdingDependencyKind.Curve)
            ]);
        Require(plan.Creates.Select(item => item.Cid).SequenceEqual(["CID-A_INT-A1"]),
            "A panel that already has curves must still get its missing curves.");
        Require(plan.Updates.Select(action => action.ObjectId).SequenceEqual([CurveA]),
            "An existing curve whose CID is still expected must be overwritten in place.");
        Require(plan.Deletes.ToHashSet().SetEquals([CurveDuplicateA, CurveStaleA]),
            "Obsolete and duplicate curves must be deleted; surfaces stay outside the Curves scope.");
        Console.WriteLine("[OK] Curves scope overwrites existing curves, adds missing ones and deletes obsolete or duplicate ones");
    }

    private static void VerifyAllScopeOverwritesExistingCurves()
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
        Require(plan.Creates.Select(item => item.Cid).OrderBy(cid => cid, StringComparer.Ordinal)
                    .SequenceEqual(["CID-A_INT-A1", "CID-B_FRM-0"]),
            "Missing curves must be created on panels with and without curves.");
        Require(plan.Updates.Select(action => action.ObjectId).ToHashSet().SetEquals([SurfaceA, CurveA]) &&
                plan.Deletes.SequenceEqual([CurveStaleA]),
            "The All scope must overwrite current surfaces and curves and delete the stale curve.");
        Console.WriteLine("[OK] All scope reconciles surfaces and curves alike");
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
        string[] required =
        {
            "if (PanelCladdingDependencyReconciliationService.Includes(scope, PanelCladdingDependencyKind.Curve))\n            {",
            "SourcePanelObjectId = owners[0].ObjectId,",
            "existing,\n                    scope);"
        };
        foreach (string token in required)
        {
            Require(source.Contains(token, StringComparison.Ordinal), $"Live PCbake is missing: {token}");
        }
        string command = Source("src", "PanelCladdingEditor", "UI", "PanelCladdingBakeCommand.cs");
        foreach (string removed in new[] { "curvePreservedPanels", "PreservedCurvePanelIds", "CurvePreservedPanels" })
        {
            Require(!source.Contains(removed, StringComparison.Ordinal) &&
                    !command.Contains(removed, StringComparison.Ordinal),
                $"Curve preservation must be gone: {removed}");
        }
        Require(!command.Contains("delete a panel's curves", StringComparison.OrdinalIgnoreCase),
            "PCbake must not ask the user to delete curves before baking.");
        Console.WriteLine("[OK] live bake plans curves for every selected panel whenever the scope includes curves");
    }

    private static void VerifyIssueDotRules()
    {
        var excluded = new PanelCladdingBakeIssue(PanelA, PanelCladdingBakeIssueService.CurveStage,
            "PANEL_CLADDING_PANEL_LAYER_EXCLUDED: ABC-IEF-Wireframe::Curves - PNL::Curves - Not in Scope");
        Require(PanelCladdingBakeIssueService.DotText(excluded) ==
                "PCbake: Curves\nPANEL_LAYER_EXCLUDED: ABC-IEF-Wireframe::Curves - PNL::Curves - Not in Scope",
            "The dot must name the stage and the reason without the PANEL_CLADDING_ prefix.");
        var noPid = new PanelCladdingBakeIssue(PanelA, PanelCladdingBakeIssueService.PidStage,
            $"PANEL_CLADDING_UPDATE_PID_REQUIRED: {PanelA:D}");
        Require(PanelCladdingBakeIssueService.DotText(noPid) == "PCbake: No PID\nUPDATE_PID_REQUIRED",
            "Object ids must be left out of the dot text.");
        Require(PanelCladdingBakeIssueService.DotText(new PanelCladdingBakeIssue(PanelA, "Duplicate CID", "")) ==
                "PCbake: Duplicate CID",
            "A dot without detail shows the stage only.");
        string longText = PanelCladdingBakeIssueService.ShortDetail(new string('X', 200));
        Require(longText.Length == 80 && longText.EndsWith('…'), "Long reasons must be shortened on the dot.");

        IReadOnlyList<PanelCladdingBakeIssue> merged = PanelCladdingBakeIssueService.Merge(
        [
            new PanelCladdingBakeIssue(PanelA, PanelCladdingBakeIssueService.NotEditableStage, "first"),
            new PanelCladdingBakeIssue(PanelA, PanelCladdingBakeIssueService.SurfaceStage, "later"),
            new PanelCladdingBakeIssue(PanelB, PanelCladdingBakeIssueService.DuplicateCidStage, "CID-B"),
            new PanelCladdingBakeIssue(Guid.Empty, PanelCladdingBakeIssueService.PidStage, "none")
        ]);
        Require(merged.Count == 2 && merged[0].Detail == "first" && merged[1].PanelObjectId == PanelB,
            "Each panel keeps one issue, the earliest one.");

        var panels = new HashSet<Guid> { PanelA };
        Require(PanelCladdingBakeIssueService.IsDotOf($" {PanelA:D} ", panels) &&
                !PanelCladdingBakeIssueService.IsDotOf(PanelB.ToString("D"), panels) &&
                !PanelCladdingBakeIssueService.IsDotOf(null, panels) &&
                !PanelCladdingBakeIssueService.IsDotOf("not a guid", panels),
            "Only dots of the panels given to the run are replaced.");
        Console.WriteLine("[OK] issue dots name the stage and a short reason; one dot per panel; a run replaces only its panels' dots");
    }

    private static void VerifyPerPanelContract()
    {
        string source = Source("src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live", "PanelCladding",
            "LivePanelCladdingUpdateService.cs").Replace("\r\n", "\n", StringComparison.Ordinal);
        string[] required =
        {
            "PanelCladdingBakeIssueService.MissingPanelStage",
            "PanelCladdingBakeIssueService.PidStage",
            "PanelCladdingBakeIssueService.NotEditableStage",
            "PanelCladdingBakeIssueService.DuplicateCidStage",
            "PanelCladdingBakeIssueService.PointOrderStage",
            "PanelCladdingBakeIssueService.SurfaceStage",
            "PanelCladdingBakeIssueService.CurveStage",
            "LivePanelCladdingCidService.Prepare(document, candidatePanelIds)",
            "RestoreOrThrow(restoreSource, source.ObjectId);",
            "IsCreatableLayerPath(document, layerPath)",
            "PANEL_CLADDING_UPDATE_DUPLICATE_EXPECTED_CID",
            "baked.Select(source => source.ObjectId).ToHashSet(),",
            "LivePanelCladdingBakeIssueDots.Mark(",
            "result.Issues = PanelCladdingBakeIssueService.Merge(issues);"
        };
        foreach (string token in required)
        {
            Require(source.Contains(token, StringComparison.Ordinal), $"Per-panel PCbake is missing: {token}");
        }
        foreach (string removed in new[]
                 {
                     "PANEL_CLADDING_UPDATE_SURFACE_PREPARE_FAILED",
                     "PANEL_CLADDING_UPDATE_CURVE_PREPARE_FAILED",
                     "Fail($\"PANEL_CLADDING_UPDATE_SOURCE_NOT_EDITABLE",
                     "Fail($\"PANEL_CLADDING_UPDATE_PID_REQUIRED",
                     "Fail(prepared.Message)"
                 })
        {
            Require(!source.Contains(removed, StringComparison.Ordinal),
                $"A problem of one panel must not fail the whole run: {removed}");
        }

        string dots = Source("src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live", "PanelCladding",
            "LivePanelCladdingBakeIssueDots.cs");
        foreach (string token in new[]
                 {
                     "ObjectTypeFilter = ObjectType.TextDot",
                     "PanelCladdingBakeIssueService.IsDotOf(",
                     "document.Objects.Delete(dot, quiet: true, ignoreModes: true)",
                     "document.Objects.AddTextDot(dot, attributes)",
                     "PanelCladdingBakeIssueService.PanelUserTextKey, issue.PanelObjectId.ToString(\"D\")"
                 })
        {
            Require(dots.Contains(token, StringComparison.Ordinal), $"Issue dots are missing: {token}");
        }

        string command = Source("src", "PanelCladdingEditor", "UI", "PanelCladdingBakeCommand.cs");
        Require(command.Contains("foreach (PanelCladdingBakeIssue issue in result.Issues)", StringComparison.Ordinal) &&
                command.Contains("PanelCladdingBakeIssueService.IssueLayerPath", StringComparison.Ordinal),
            "PCbake must report every skipped panel and where its dot is.");
        Console.WriteLine("[OK] live bake skips a failing panel whole, restores its source writes and marks it; the batch goes on");
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
