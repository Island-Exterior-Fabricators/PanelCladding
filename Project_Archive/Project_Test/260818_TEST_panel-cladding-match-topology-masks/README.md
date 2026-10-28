# Panel cladding match topology-mask smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-match-topology-masks`](../../../tests/Smoke/panel-cladding-match-topology-masks/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-match-topology-masks`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

This focused smoke verifies that `PCMatchSrf` ignores the two panel-level topology masks
while preserving target-owned H/V divider offsets. It also covers different source/target masks,
sources without masks, unrelated target-data preservation, and fail-closed grid-dimension compatibility.

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-match-topology-masks\PanelCladdingMatchTopologyMasksSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-match-topology-masks\PanelCladdingMatchTopologyMasksSmoke.csproj -c Release
```

The smoke is planner-level and does not mutate Rhino, the plug-in installation, workbooks, or user
registry state.

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../../../Project_Test/261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
