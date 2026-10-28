# PCMatchSrf Logical Cell Cleanup TEST

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/pcmatch-srf-logical-cell-cleanup`](../../tests/Smoke/pcmatch-srf-logical-cell-cleanup/) (run `.\tests\Run-Regression.ps1 -Name pcmatch-srf-logical-cell-cleanup`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Runs a standalone regression for source merge topology that hides physical cells `0B` and `1C`.

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-logical-cell-cleanup\PCMatchSrfLogicalCellCleanupSmoke.csproj -c Debug
```

The test verifies that PCMatchSrf deletes stale hidden target keys without recreating them, retains surviving blank and parent-cell assignments, and preserves target-owned layout/topology metadata.

## Verified result

- Debug: exit 0
- Release: exit 0
- Hidden target keys `CW_4.00_CLADDING_0B` and `CW_4.01_CLADDING_1C` are deleted and absent after applying the plan.
- Target offsets, segment/merge masks, type/signature metadata, and unrelated user text remain unchanged.

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
