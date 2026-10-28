# Panel cladding match parent-cell smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-match-parent-cells`](../../../tests/Smoke/panel-cladding-match-parent-cells/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-match-parent-cells`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

This focused smoke reproduces the reported 2x2 parent-cell configuration:

- `0A=MPL-001`
- `0B=MPL-001`
- `1A=0A`
- `1B=0B`

It verifies that `PCMatchSrf` preserves the two child references, reconstructs two two-cell parent
regions after applying/reparsing the plan, rejects a reference cycle before target mutation, keeps
segment/merge mask transfer, and does not copy or delete target H/V offsets.

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-match-parent-cells\PanelCladdingMatchParentCellsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-match-parent-cells\PanelCladdingMatchParentCellsSmoke.csproj -c Release
```

The smoke is application/planner-level and does not mutate Rhino, a workbook, registry state, or the
plug-in installation.

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../../../Project_Test/261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
