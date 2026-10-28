# PCMatchSrf logical-cell smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/pcmatch-srf-logical-cells`](../../../tests/Smoke/pcmatch-srf-logical-cells/) (run `.\tests\Run-Regression.ps1 -Name pcmatch-srf-logical-cells`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Run:

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-logical-cells\PCMatchSrfLogicalCellsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-logical-cells\PCMatchSrfLogicalCellsSmoke.csproj -c Release
```

The smoke verifies the `PCMatchSrf` public command name, cell-code-only compatibility, missing-source-cell blank writes, exact parent graph transfer, preservation of target offsets/masks/type/signature, and fail-closed row/column or parent-reference mismatches.

Since 1.2.4 a `PCMatchSrf` source must be configured: the blank sources carry an all-blank logic, an unconfigured source is asserted refused, an invalid reference fails as `PANEL_CLADDING_LOGIC_PARENT_UNKNOWN`, and fixtures go through `CladdingLogicFixture.WithLogic` (see [the logic authority TEST](../../../Project_Test/261027_TEST_panel-cladding-logic-authority/README.md)). Results recorded above predate that change.
