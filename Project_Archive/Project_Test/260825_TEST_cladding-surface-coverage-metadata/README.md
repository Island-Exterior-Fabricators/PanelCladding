# Cladding Surface Coverage Metadata Regression

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/cladding-surface-coverage-metadata`](../../../tests/Smoke/cladding-surface-coverage-metadata/) (run `.\tests\Run-Regression.ps1 -Name cladding-surface-coverage-metadata`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates `Merge_Mark` encoding, spawn persistence, sync migration/refresh, fail-closed metadata validation, and metadata-authoritative hidden-mullion offset behavior.

Run from the repository root:

```powershell
dotnet run --project Project_Test/260825_TEST_cladding-surface-coverage-metadata/CladdingSurfaceCoverageMetadataSmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_cladding-surface-coverage-metadata/CladdingSurfaceCoverageMetadataSmoke.csproj -c Release
```

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../../../Project_Test/261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
