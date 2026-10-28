# Panel Cladding Extrusion Fidelity Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-extrusion-fidelity`](../../tests/Smoke/panel-cladding-extrusion-fidelity/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-extrusion-fidelity`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Run:

```powershell
dotnet run --project Project_Test/260813_TEST_panel-cladding-extrusion-fidelity/PanelCladdingExtrusionFidelitySmoke.csproj -c Release
```

The smoke verifies unit-free unclipped divider offsets, curve-centered badges, identical cladding and
extrusion base stroke bands, and the developed yellow-halo/dark-core extrusion selection treatment.
