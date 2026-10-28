# Panel Cladding Canvas Alignment Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-canvas-alignment`](../../../tests/Smoke/panel-cladding-canvas-alignment/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-canvas-alignment`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Run:

```powershell
dotnet run --project Project_Test/260813_TEST_panel-cladding-canvas-alignment/PanelCladdingCanvasAlignmentSmoke.csproj -c Release
```

The smoke verifies removal of the bottom instruction legend, far-right Clear/zoom ordering and danger
styling, and true panel-to-canvas center alignment in cladding and extrusion views at desktop and
compact window sizes.
