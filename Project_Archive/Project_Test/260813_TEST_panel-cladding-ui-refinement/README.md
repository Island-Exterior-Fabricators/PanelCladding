# Panel Cladding UI Refinement Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-ui-refinement`](../../../tests/Smoke/panel-cladding-ui-refinement/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-ui-refinement`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Runs a headless WPF fixture for the ten refinements requested on 2026-08-13. It verifies the compact
panel-information card, exact five-decimal offsets, active-state colors, rounded assignment fields,
red translucent cell selection, inline dimension editing, panel-facing witness marks, redundant-UI
removal, and the continued absence of Eto/WebView dependencies.

Run from the repository root:

```powershell
dotnet run --project Project_Test/260813_TEST_panel-cladding-ui-refinement/PanelCladdingUiRefinementSmoke.csproj -c Release
```

The smoke writes three PNGs in this directory for visual inspection.
