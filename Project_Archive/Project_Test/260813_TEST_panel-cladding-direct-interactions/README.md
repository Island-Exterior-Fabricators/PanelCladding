# Panel Cladding Direct Interactions Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-direct-interactions`](../../../tests/Smoke/panel-cladding-direct-interactions/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-direct-interactions`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Run:

```powershell
dotnet run --project Project_Test/260813_TEST_panel-cladding-direct-interactions/PanelCladdingDirectInteractionsSmoke.csproj -c Release
```

The smoke exercises blank deselection in both views, material/parent dropdown preview assignment,
editable Current Panel divider offsets, zero Rhino commits before Save, 80–150% zoom and pan clamps,
dimension lock/label placement, toolbar/footer alignment, 10-inch grid logic, half-weight line sources,
and 1440×900 cladding/extrusion renders.
