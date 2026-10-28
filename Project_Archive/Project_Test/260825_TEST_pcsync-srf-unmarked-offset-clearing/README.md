# PCSyncSrf Unmarked Offset Clearing Regression

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/pcsync-srf-unmarked-offset-clearing`](../../../tests/Smoke/pcsync-srf-unmarked-offset-clearing/) (run `.\tests\Run-Regression.ps1 -Name pcsync-srf-unmarked-offset-clearing`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates that unmarked cladding surfaces cannot preserve geometry-missing stored offsets, while complete baked merged coverage remains authoritative.

Run from the repository root:

```powershell
dotnet run --project Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/PCSyncSrfUnmarkedOffsetClearingSmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/PCSyncSrfUnmarkedOffsetClearingSmoke.csproj -c Release
```
