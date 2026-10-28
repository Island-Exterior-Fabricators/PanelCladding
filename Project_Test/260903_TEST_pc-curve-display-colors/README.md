# Panel Cladding Curve Display Colors Regression

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/pc-curve-display-colors`](../../tests/Smoke/pc-curve-display-colors/) (run `.\tests\Run-Regression.ps1 -Name pc-curve-display-colors`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

This regression verifies the authoritative color mapping (Blue main frame, Purple horizontal
intermediate, DarkGreen vertical intermediate), propagation onto extrusion plans, existing-color
drift detection, and Rhino attribute writes across PCSpawnCrv, PCUpdate, and PCSyncCrv.

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pc-curve-display-colors\PCCurveDisplayColorsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pc-curve-display-colors\PCCurveDisplayColorsSmoke.csproj -c Release
```
