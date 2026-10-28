# PCMatchSrf Repair Existing Targets TEST

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/pcmatch-srf-repair-existing-targets`](../../tests/Smoke/pcmatch-srf-repair-existing-targets/) (run `.\tests\Run-Regression.ps1 -Name pcmatch-srf-repair-existing-targets`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates that PCMatchSrf can overwrite a partially configured compatible target, restore a missing parent value such as `1A=0A`, remove stale target-only cell keys, preserve all non-cell attributes, and detect incomplete Rhino attribute commits before reporting success.

Run with:

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-repair-existing-targets\PCMatchSrfRepairExistingTargetsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-repair-existing-targets\PCMatchSrfRepairExistingTargetsSmoke.csproj -c Release
```

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
