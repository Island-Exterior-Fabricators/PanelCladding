# Standalone panel cladding editor smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/standalone-panel-cladding-editor`](../../tests/Smoke/standalone-panel-cladding-editor/) (run `.\tests\Run-Regression.ps1 -Name standalone-panel-cladding-editor`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Run independently of MCP_Rhino.Server:

```powershell
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Release
```

The smoke verifies canonical type/signature keys, key parsing, curved projection, deterministic v2
cell/material-only identity independent of dimensions, geometry, depth, units, and H/V offsets;
preview rendering, Open XML catalog behavior, command registration, and absence of MCP assembly
references.

The project reference sets `PanelCladdingTestHost=true`, asking MSBuild for a DLL-form test assembly
because the `dotnet` console host resolves project references by DLL convention. Default and
published production builds remain direct `.rhp` outputs.

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
