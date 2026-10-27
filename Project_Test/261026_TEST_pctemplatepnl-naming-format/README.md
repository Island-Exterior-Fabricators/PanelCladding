# PCtemplatePNL per-project PNL format TEST

Plan: [261026_PLAN_pctemplatepnl-naming-format](../../Project_Plan/261026_PLAN_pctemplatepnl-naming-format.md)

## Managed regression

The checks live in the naming-convention smoke, which already owns the PNL sheet code:
[`261023_TEST_panel-cladding-naming-convention`](../261023_TEST_panel-cladding-naming-convention/README.md)
(`Program.cs` and `Program.Template.cs`). They cover:

- format tokens `AA`, `A`, `A1`, `01`, `001`; sequences without I/O; the two-character
  overflow `01`..`99`, `A1`..`Z9`, `AA`..`ZZ`; format text round trip and rejection;
- frame-config codes per format (`A`, `01`, `001` in order; `AA`, `A1` grouped by intermediate
  counts), stored codes of another format skipped, exhaustion reported; a one-letter code without
  `CW_9.02` reports `PANEL_FRAME_CONFIG_BACKUP_MISSING`;
- wall-type numbering in the wall format and the report when no code is free;
- the sync writing in place: codes keep their rows, placeholders are filled, a written value is
  never replaced, foreign rows, merges and numbers are kept, struck state only, no-op detection;
- PCtemplatePNL on a XYZ-like sheet (only J14–J36 and H26–H36 placeholders and the format are
  added, every other cell unchanged, idempotent, the sync then byte-for-byte unchanged, a new
  configuration and wall type fill their placeholder rows in place) and a ABC-like sheet
  (Cladding widened to N:P and narrowed back: cells, merges and widths move; titles and
  placeholders added; placeholders renumbered); refusals for codes that do not fit, formulas
  (no shift) and missing headers; Definition left alone when its codes do not fit.

`260818_TEST_panel-cladding-pc-commands` expects the thirteenth command; the installer,
registration test and first-release installation test list `PCtemplatePNL`.

```powershell
dotnet run --project Project_Test/261023_TEST_panel-cladding-naming-convention/PanelCladdingNamingConventionSmoke.csproj -c Debug
dotnet run --project Project_Test/261023_TEST_panel-cladding-naming-convention/PanelCladdingNamingConventionSmoke.csproj -c Release
```

## XYZ sheet (live project data)

A scratch console (session scratchpad, references the plug-in with `PanelCladdingTestHost=true`)
runs `OpenXmlPanelNamingTemplate.Inspect/Apply` with `A`, `A`, `01`, `001`, `01` on a copy of
`XYZ - Naming Convention.xlsx`, then the save-time sync path (`Read`, a registry with the
sheet's Frame-Config rows and the STN panels' `A` = `0H 0V`, `BuildTables` with the STN
usage F/L, A, 1A.1/1A.2/1C.1/1C.2/1C.4/3A.2, `Write`). A Python/openpyxl comparison lists
every cell's value, type, strike, colour, bold, fill, number format, alignment and border, the
merges, column widths and custom properties before and after. Results are in the EXET.

The save hook, PCtemplatePNL prompts and the panel write path run inside Rhino and are
recorded in the EXET; a build does not prove them.
