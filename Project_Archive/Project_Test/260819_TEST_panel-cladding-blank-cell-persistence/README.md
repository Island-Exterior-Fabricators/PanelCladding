# Panel cladding blank-cell persistence smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-blank-cell-persistence`](../../../tests/Smoke/panel-cladding-blank-cell-persistence/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-blank-cell-persistence`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Run:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-blank-cell-persistence\PanelCladdingBlankCellPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-blank-cell-persistence\PanelCladdingBlankCellPersistenceSmoke.csproj -c Release
```

The smoke verifies that PCEditor Save retains every surviving unassigned logical cladding key with a Rhino-storable blank value, that the parser and type identity treat the stored value as empty, and that hidden physical members of merged cells remain obsolete.
