# Panel Cladding Surface Sync Structural Grid TEST

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-surface-sync-structural-grid`](../../tests/Smoke/panel-cladding-surface-sync-structural-grid/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-surface-sync-structural-grid`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates the `PCSyncSrf` condition where a structural mullion remains in the panel but one cladding surface spans across that mullion.

The smoke test verifies:

- surface-plus-curve inference preserves the structural `V0` track;
- the live `PCSyncSrf` adapter supplies associated curves to that inference path;
- a spanning cladding still remains one surface region;
- the owner cell receives the material and the covered adjacent cell is reconstructed as `1A=0A`.

Run with:

```powershell
dotnet run --project Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/PanelCladdingSurfaceSyncStructuralGridSmoke.csproj -c Debug
dotnet run --project Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/PanelCladdingSurfaceSyncStructuralGridSmoke.csproj -c Release
```
