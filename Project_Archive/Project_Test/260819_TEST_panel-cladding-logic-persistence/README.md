# Panel Cladding Logic Persistence Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-logic-persistence`](../../../tests/Smoke/panel-cladding-logic-persistence/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-logic-persistence`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates the material-free `CW_2.13_CLADDING_LOGIC` contract and its use by `PCSyncSrf`.

Coverage includes:

- deterministic material-free JSON encoding and strict graph validation;
- saved-owner preservation for unchanged geometry;
- geometry-authoritative owner fallback for surface merges and splits;
- legacy panels with no logic attribute and fail-closed malformed payloads;
- sync backfill plus commit postcondition validation;
- editor Save, `PCCreate`, `PCMatchSrf`, and `PCClear` maintenance of the attribute.

Run from the repository root:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logic-persistence\PanelCladdingLogicPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logic-persistence\PanelCladdingLogicPersistenceSmoke.csproj -c Release
```

Since 1.2.4 the logic carries owner materials (`{"0A":"MPL-001","1A":"0A"}`), `PCCreate` writes no logic and a `PCMatchSrf` source needs logic; the expected payloads were updated (see [the logic authority TEST](../../../Project_Test/261027_TEST_panel-cladding-logic-authority/README.md)). Results recorded above predate that change.
