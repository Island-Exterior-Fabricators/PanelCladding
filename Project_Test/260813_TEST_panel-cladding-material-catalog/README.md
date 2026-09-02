# Panel Cladding Material Catalog Smoke

Run:

```powershell
dotnet run --project Project_Test/260813_TEST_panel-cladding-material-catalog/PanelCladdingMaterialCatalogSmoke.csproj -c Release
```

The smoke verifies:

- creation and round-trip reading of a seven-entry `Materials` worksheet;
- table filtering, frozen header, exact ABC colors, and absence of generated type sheets;
- panel save without a workbook, including the Rhino type-code write and legacy signature cleanup;
- sidebar ordering, `CLADDING TYPE` naming, bright-red selection overwrite, and centered dash source contract.

`extract_abc_materials.py` is a read-only migration helper. It reads the ABC `.3dm` with `rhino3dm`,
collects material codes used by panel metadata, matches them to material-layer colors, and emits
`abc-materials.json`. It does not launch or modify Rhino.

The ABC workbook was separately rendered, migrated, rendered again, re-imported, value-inspected,
and scanned for formula errors through the spreadsheet artifact runtime. A timestamped sibling backup
was created before replacement.
