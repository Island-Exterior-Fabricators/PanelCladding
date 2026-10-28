# Panel Cladding Scoped Save Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-scoped-save`](../../tests/Smoke/panel-cladding-scoped-save/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-scoped-save`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates absent topology masks as the default `PCSpawnCrv` state, sparse default persistence, and
the editor's three save scopes. Every scope removes all four exact retired cladding
type aliases (including case variants), returns no type code, and preserves unrelated
CAD type metadata. The UI exposes no cladding type preview and retains frame typology.

Run:

```powershell
dotnet run --project Project_Test/260819_TEST_panel-cladding-scoped-save/PanelCladdingScopedSaveSmoke.csproj -c Debug
dotnet run --project Project_Test/260819_TEST_panel-cladding-scoped-save/PanelCladdingScopedSaveSmoke.csproj -c Release
```
