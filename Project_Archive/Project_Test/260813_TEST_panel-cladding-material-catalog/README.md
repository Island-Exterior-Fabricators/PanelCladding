# Panel Cladding Material Catalog Smoke

> **Client data removed on 2026-11-04.** Files this record produced from client project models (exports, screenshots, object and layer dumps) were removed from the repository and its history, and project codes and paths here were replaced with fictional ones; see the [client data cleanup EXET](../../../Project_Exet/261104_EXET_client-data-cleanup.md). The conclusions below stand; links to the removed files no longer resolve.

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-material-catalog`](../../../tests/Smoke/panel-cladding-material-catalog/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-material-catalog`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Run:

```powershell
dotnet run --project Project_Test/260813_TEST_panel-cladding-material-catalog/PanelCladdingMaterialCatalogSmoke.csproj -c Release
```

The smoke verifies:

- creation and round-trip reading of a seven-entry `Materials` worksheet;
- table filtering, frozen header, exact ABC colors, and absence of generated type sheets;
- panel save without a workbook, with no cladding type-code write and legacy signature cleanup;
- sidebar ordering, `CLADDING TYPE` naming, bright-red selection overwrite, and centered dash source contract.

`extract_abc_materials.py` is a read-only migration helper. It reads the ABC `.3dm` with `rhino3dm`,
collects material codes used by panel metadata, matches them to material-layer colors, and emits
`abc-materials.json`. It does not launch or modify Rhino.

The ABC workbook was separately rendered, migrated, rendered again, re-imported, value-inspected,
and scanned for formula errors through the spreadsheet artifact runtime. A timestamped sibling backup
was created before replacement.
