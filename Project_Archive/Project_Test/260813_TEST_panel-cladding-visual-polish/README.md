# Panel Cladding Visual Polish Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-visual-polish`](../../../tests/Smoke/panel-cladding-visual-polish/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-visual-polish`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Run:

```powershell
dotnet run --project Project_Test/260813_TEST_panel-cladding-visual-polish/PanelCladdingVisualPolishSmoke.csproj -c Release
```

The smoke verifies unmodified right-button pan, material-backed 1.5-DIP parent dashes without a white
halo, rounded temporary green focus styling for Current Panel offsets, compact setup-dialog control
tokens, and deterministic canvas/dialog renders.
