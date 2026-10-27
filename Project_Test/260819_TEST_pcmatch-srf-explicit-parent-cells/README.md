# PCMatchSrf Explicit Parent Cells TEST

Regression coverage for copying the reported 2-column by 4-row parent graph and for preserving an explicitly stored parent-cell key even when extrusion topology would otherwise collapse that physical cell.

Run with:

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-explicit-parent-cells\PCMatchSrfExplicitParentCellsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-explicit-parent-cells\PCMatchSrfExplicitParentCellsSmoke.csproj -c Release
```

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
