# Panel extrusion assignment smoke test

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-extrusion-assignment`](../../../tests/Smoke/panel-extrusion-assignment/) (run `.\tests\Run-Regression.ps1 -Name panel-extrusion-assignment`); `panel-extrusion-native-runtime` probe moved to [`tests/Probes/panel-extrusion-native-runtime`](../../../tests/Probes/panel-extrusion-native-runtime/). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

Validates additive main-frame extrusion assignments, deterministic frame typology, exact Rhino
attribute contracts, the production PDF schedule importer against the ABC framing schedule,
and transactional round-tripping through the workbook `Extrusions` worksheet.

Run with:

```powershell
dotnet run --project Project_Test/260820_TEST_panel-extrusion-assignment/PanelExtrusionAssignmentSmoke.csproj -- "C:\Projects\ABC\Design\04-Shop-DWGs\03-Cover-General-Notes\F0-020-EXTRUSION-SCHEDULE.pdf"
```

The test writes the imported `ALU-H0651` crop, a QA workbook, the main extrusion view, and the
secondary setup window under its build output `qa` directory for visual and data inspection.

After packaging, verify Rhino-style top-level native resolution with:

```powershell
dotnet run --project Project_Test/260820_TEST_panel-extrusion-assignment/NativeRuntimeProbe/PanelExtrusionNativeRuntimeProbe.csproj -- Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.62/Plugin "V:\project\extrusion-schedule.pdf"
```
