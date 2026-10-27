# Cladding Surface Coverage Metadata Regression

Validates `Merge_Mark` encoding, spawn persistence, sync migration/refresh, fail-closed metadata validation, and metadata-authoritative hidden-mullion offset behavior.

Run from the repository root:

```powershell
dotnet run --project Project_Test/260825_TEST_cladding-surface-coverage-metadata/CladdingSurfaceCoverageMetadataSmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_cladding-surface-coverage-metadata/CladdingSurfaceCoverageMetadataSmoke.csproj -c Release
```

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
