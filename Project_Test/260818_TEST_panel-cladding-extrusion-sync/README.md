# Panel Cladding Extrusion Spawn and Geometry Sync Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-extrusion-sync`](../../tests/Smoke/panel-cladding-extrusion-sync/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-extrusion-sync`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-extrusion-sync\PanelCladdingExtrusionSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-extrusion-sync\PanelCladdingExtrusionSyncSmoke.csproj -c Release
```

The executable verifies topology-to-curve expansion, underscore naming, PID/CRV/CID metadata,
the new/legacy surface-root contract, the `PCSync` command name, metadata repair planning that does
not trust old object attributes, and five-decimal unit dimensions.
