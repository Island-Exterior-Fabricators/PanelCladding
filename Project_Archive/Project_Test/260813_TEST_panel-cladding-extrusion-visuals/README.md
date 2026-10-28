# Panel Cladding Extrusion Visuals Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-extrusion-visuals`](../../../tests/Smoke/panel-cladding-extrusion-visuals/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-extrusion-visuals`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Run:

```powershell
dotnet run --project Project_Test/260813_TEST_panel-cladding-extrusion-visuals/PanelCladdingExtrusionVisualsSmoke.csproj -c Release
```

The smoke verifies shared cladding/extrusion line weights, bright-yellow overwrite selection, compact
two-group structural actions, hollow intersections, midpoint extrusion labels, 60-DIP dimension
clearance, muted material-code cell labels, and deterministic cladding/extrusion renders.
