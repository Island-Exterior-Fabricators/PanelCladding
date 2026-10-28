# Extrusion assignment drop-zone smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/extrusion-assignment-dropzone`](../../../tests/Smoke/extrusion-assignment-dropzone/) (run `.\tests\Run-Regression.ps1 -Name extrusion-assignment-dropzone`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates that extrusion catalogue tiles are drag-only, the assignment section is the only profile
drop target, assigned cards carry profile previews and inline curve modifiers, and the available
catalogue is filtered against the selected curves' assigned codes.

Run:

```powershell
dotnet run --project Project_Test/260826_TEST_extrusion-assignment-dropzone/ExtrusionAssignmentDropZoneSmoke.csproj
```

The richer WPF interaction and render fixture remains in
`Project_Test/260820_TEST_panel-extrusion-assignment` and is run alongside this focused contract test.
