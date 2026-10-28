# Cladding Merge Mark Key Regression

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/cladding-merge-mark-key`](../../../tests/Smoke/cladding-merge-mark-key/) (run `.\tests\Run-Regression.ps1 -Name cladding-merge-mark-key`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates canonical `Merge_Mark` spawn/sync persistence, legacy-key migration, conflict rejection, and removal of `CW_1.03_CLADDING_CELLS` from new baked output.

Run from the repository root:

```powershell
dotnet run --project Project_Test/260825_TEST_cladding-merge-mark-key/CladdingMergeMarkKeySmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_cladding-merge-mark-key/CladdingMergeMarkKeySmoke.csproj -c Release
```

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../../../Project_Test/261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
