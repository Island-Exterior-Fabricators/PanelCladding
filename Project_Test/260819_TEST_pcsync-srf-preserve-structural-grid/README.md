# PCSyncSrf Preserve Structural Grid Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/pcsync-srf-preserve-structural-grid`](../../tests/Smoke/pcsync-srf-preserve-structural-grid/) (run `.\tests\Run-Regression.ps1 -Name pcsync-srf-preserve-structural-grid`). Commands below are historical; see the [regression test layout EXET](../../Project_Archive/Project_Exet/261028_EXET_regression-test-layout.md).

Reproduces the saved ABC condition where a panel contains `V0=22.5` and `1A=0A`, while its spanning cladding surface exposes no vertical boundary and no associated extrusion curve exists.

Validates:

- `PCSyncSrf` preserves stored structural offsets;
- newly inferred surface boundaries are unioned without duplicates;
- `PCSyncCrv` remains authoritative for offset removal;
- the preserved two-column grid reconstructs `1A=0A`;
- the live repository uses the scope-aware offset set for grid construction.

Run:

```powershell
dotnet run --project Project_Test/260819_TEST_pcsync-srf-preserve-structural-grid/PCSyncSrfPreserveStructuralGridSmoke.csproj -c Debug
```
