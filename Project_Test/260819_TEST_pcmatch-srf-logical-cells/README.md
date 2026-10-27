# PCMatchSrf logical-cell smoke

Run:

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-logical-cells\PCMatchSrfLogicalCellsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-logical-cells\PCMatchSrfLogicalCellsSmoke.csproj -c Release
```

The smoke verifies the `PCMatchSrf` public command name, cell-code-only compatibility, missing-source-cell blank writes, exact parent graph transfer, preservation of target offsets/masks/type/signature, and fail-closed row/column or parent-reference mismatches.

Since 1.2.4 a `PCMatchSrf` source must be configured: the blank sources carry an all-blank logic, an unconfigured source is asserted refused, an invalid reference fails as `PANEL_CLADDING_LOGIC_PARENT_UNKNOWN`, and fixtures go through `CladdingLogicFixture.WithLogic` (see [the logic authority TEST](../261027_TEST_panel-cladding-logic-authority/README.md)). Results recorded above predate that change.
