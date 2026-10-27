# Panel cladding match topology-mask smoke

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

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
