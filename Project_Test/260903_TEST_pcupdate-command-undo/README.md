# PCUpdate Command Undo Regression

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/pcupdate-command-undo`](../../tests/Smoke/pcupdate-command-undo/) (run `.\tests\Run-Regression.ps1 -Name pcupdate-command-undo`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

This focused regression verifies that `PCUpdate` reuses Rhino's active command Undo record instead
of requesting a nested record. It also verifies that close/automatic rollback operations remain
limited to records opened by the update service and that the corrected plug-in is patch-versioned.

Run the static/contract smoke in both configurations:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Release
```

Run the native ambient-record probe in a supported Rhino test host:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Release -- --rhino-undo
```
