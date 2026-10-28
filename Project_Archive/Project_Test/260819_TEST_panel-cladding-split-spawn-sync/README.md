# Panel Cladding Split Spawn/Sync Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-split-spawn-sync`](../../../tests/Smoke/panel-cladding-split-spawn-sync/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-split-spawn-sync`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates that the combined `PCSpawn` and `PCSync` commands are absent, the four scoped surface and
curve commands are registered, and curve planning remains independent of cladding assignments.

Run with:

```powershell
dotnet run --project Project_Test/260819_TEST_panel-cladding-split-spawn-sync/PanelCladdingSplitSpawnSyncSmoke.csproj -c Debug
```
