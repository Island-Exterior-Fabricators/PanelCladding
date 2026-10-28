# Panel Cladding Layout Reconciliation TEST

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-layout-reconciliation`](../../tests/Smoke/panel-cladding-layout-reconciliation/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-layout-reconciliation`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Focused regression coverage for the general layout reconciliation contract:

- coordinate/order-based topology remapping across inserted and moved H/V tracks;
- preservation and expansion of missing, hidden, and merged atoms;
- semantic `PCMatchSrf` matching across different raw grid dimensions;
- rejection when target `CW_2.12_DELETE_MASK` makes a required cladding boundary impossible;
- retirement of canonical and legacy persisted Signature keys;
- an explicit guard against fixture-specific production branches.

Run:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-layout-reconciliation\PanelCladdingLayoutReconciliationSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-layout-reconciliation\PanelCladdingLayoutReconciliationSmoke.csproj -c Release
```

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
