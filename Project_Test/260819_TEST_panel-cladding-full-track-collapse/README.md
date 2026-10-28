# Panel cladding full-track collapse smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-full-track-collapse`](../../tests/Smoke/panel-cladding-full-track-collapse/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-full-track-collapse`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates complete H/V extrusion-track removal, cell/parent renumbering, Save cleanup and unit dimensions, immediate editor redraw state, and PCSpawnSrf logical-region expansion.

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-full-track-collapse\PanelCladdingFullTrackCollapseSmoke.csproj -c Debug
```
