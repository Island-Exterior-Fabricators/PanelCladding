# Cladding Merge Mark Key Regression

Validates canonical `Merge_Mark` spawn/sync persistence, legacy-key migration, conflict rejection, and removal of `CW_1.03_CLADDING_CELLS` from new baked output.

Run from the repository root:

```powershell
dotnet run --project Project_Test/260825_TEST_cladding-merge-mark-key/CladdingMergeMarkKeySmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_cladding-merge-mark-key/CladdingMergeMarkKeySmoke.csproj -c Release
```

Since 1.2.4 the plug-in reads cladding only from `UD_CLADDING_LOGIC`; this smoke passes its `CW_4.xx` fixtures through `CladdingLogicFixture.WithLogic` (linked from [the logic authority TEST](../261027_TEST_panel-cladding-logic-authority/README.md)), so its assertions are unchanged. Results recorded above predate that change.
