# PCSyncSrf Parent Persistence Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/pcsync-srf-parent-persistence`](../../tests/Smoke/pcsync-srf-parent-persistence/) (run `.\tests\Run-Regression.ps1 -Name pcsync-srf-parent-persistence`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates the complete surface-sync path from a spanning cladding surface through the final panel commit request.

- the exact geometry-derived graph persists as `0A=MPL-001`, `1A=0A`;
- signature normalization cannot replace the persisted parent reference;
- every logical cell is required and blank cells use the persisted-blank sentinel;
- postcondition validation rejects a missing or changed parent value.

Run:

```powershell
dotnet run --project Project_Test/260819_TEST_pcsync-srf-parent-persistence/PCSyncSrfParentPersistenceSmoke.csproj -c Debug
```
