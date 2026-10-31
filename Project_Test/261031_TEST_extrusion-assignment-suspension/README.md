# Extrusion assignment suspension TEST

Evidence for [the PLAN](../../Project_Plan/261031_PLAN_extrusion-assignment-suspension.md):
PCEditor's Extrusion view edits the layout only; curve profile assignment is suspended behind
a private switch in `PanelCladdingEditorWindow`.

## Managed smoke

```powershell
.\tests\Run-Regression.ps1 -Name extrusion-assignment-suspension
```

[`tests/Smoke/extrusion-assignment-suspension`](../../tests/Smoke/extrusion-assignment-suspension/)
loads a 2000 × 1600 mm panel with one horizontal and one vertical mullion whose saved
assignments put `1D-T000` on `FRM-0` and `1D-T001` on the first horizontal segment. It checks:

- the switch is off by default;
- Extrusion view hides the profile catalogue, the FRAME CONFIGURATION note and tooltip about
  profiles, and the floating assignment panel for frame, intermediate and merged selections;
  the canvas receives no assigned codes for its curve labels;
- FRAME CONFIGURATION and the layout actions stay visible; Merge on the two horizontal segments
  works and Undo reverts it; Hide on a vertical segment works;
- Save Extrusions commits `UD_FRAME_ASSIGNMENTS` with both existing codes and a topology of one
  merged run and one hidden segment;
- Cladding view still shows the material legend and cell assignment;
- with the switch turned on, a new editor shows the catalogue, the floating panel, the note
  and `1D-T000` on `FRM-0` again. The switch is reset afterwards.

Renders (from `artifacts/tests/extrusion-assignment-suspension/`):

- `extrusion-view-layout-only-1440x900.png`: both horizontal segments selected, no
  assignment panel or catalogue.
- `extrusion-view-merged-hidden-1440x900.png`: after the merge and hide; curve labels carry
  no profile codes.

## Updated smokes

`pc-editor-canvas-assignment` and the real-schedule UI path of `panel-extrusion-assignment`
turn the switch on before building the editor, so the dormant assignment UI stays covered.
`extrusion-assignment-dropzone` is a source check and passes unchanged.

## Results (2026-10-31, NOVAN-HOME)

| Run | Result |
| --- | --- |
| `Run-Regression.ps1` Debug | 78/78 passed ([`regression-Debug.json`](regression-Debug.json)) |
| `Run-Regression.ps1` Release | 78/78 passed ([`regression-Release.json`](regression-Release.json)) |
| Production RHP build, Debug and Release | 0 warnings, 0 errors |
| `Verify-PluginAssemblyIdentity.ps1` Debug and Release | `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, one distinct id |

`panel-extrusion-assignment` ran without `PANEL_EXTRUSION_SCHEDULE_PDF`, so its real-schedule
UI path (`VerifyUi`) was compiled but not executed. `pc-editor-canvas-assignment` exercises
the same assignment UI with the switch on.

## Not run

Live acceptance in Rhino needs this build installed through the attested channel. No package
was built, nothing was installed, and no Windows UI automation was used.
