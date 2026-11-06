# Extrusion assignment suspension EXET

## Corresponding plan and execution date

[PLAN](../Project_Plan/261031_PLAN_extrusion-assignment-suspension.md),
[TEST evidence](../Project_Test/261031_TEST_extrusion-assignment-suspension/README.md).
Executed 2026-10-31 on branch `codex/extrusion-assignment-suspension` from `master` (`ad23285`),
host NOVAN-HOME; commit `a906641`, [PR 9](https://github.com/nuofan-island/PanelCladding/pull/9).
Version 1.2.5 is unchanged.

## Changes

- `PanelCladdingEditorWindow.xaml.cs`: a private static `_extrusionAssignmentEnabled` switch,
  `false`, gates the assignment surface:
  - `SwitchView` keeps `ExtrusionLegendSection` (catalogue and Setup) collapsed;
  - `UpdateExtrusionSelectionUi` keeps the floating `ExtrusionAssignmentSection` collapsed for
    any selection, so its drop target is unreachable;
  - `VisibleAssignmentMap` gives the canvas an empty map, so curve labels show no profile codes;
  - `UpdateFrameConfigurationPreview` drops the assigned-profile tooltip, and the constructor
    collapses the new `FrameConfigurationNote`.
- `PanelCladdingEditorWindow.xaml`: the FRAME CONFIGURATION note is named `FrameConfigurationNote`.
- Assignment handlers, catalogue loading, assignment state capture, layout remapping, save and
  every command are unchanged.
- New smoke `tests/Smoke/extrusion-assignment-suspension`.
- `pc-editor-canvas-assignment` and the `VerifyUi` path of `panel-extrusion-assignment` set the
  switch on through reflection before building the editor.
- `Packaging/PanelCladdingEditor/README.md` describes the suspension.

## Deviations from plan

None. The PLAN's file list was corrected before execution finished: the static
`extrusion-assignment-dropzone` check needed no change.

## Discovered issues

- Merge still refuses segments whose existing length modifiers differ. While assignment is
  suspended a user cannot change those modifiers, so such a run cannot be merged.
- `panel-extrusion-assignment` runs its editor UI path only with a real schedule PDF
  (`PANEL_EXTRUSION_SCHEDULE_PDF`), which was not set here.

## Commands and results

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug     # 0 warnings, 0 errors
.\tests\Run-Regression.ps1 -Configuration Debug -Name extrusion-assignment-suspension   # PASS
.\tests\Run-Regression.ps1 -Configuration Debug -Name pc-editor-canvas-assignment,panel-extrusion-assignment,extrusion-assignment-dropzone,panel-cladding-hide-mask,panel-cladding-full-track-collapse,panel-cladding-direct-interactions,panel-cladding-topology-persistence,panel-cladding-scoped-save,panel-cladding-extrusion-visuals   # 9/9
.\tests\Run-Regression.ps1                                                   # Debug 78/78, Release 78/78
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug     # 0 warnings, 0 errors
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release   # 0 warnings, 0 errors
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug       # 7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -Configuration Release     # 7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35
```

Both production RHPs were written after the last source edit and contain the new switch.

## Acceptance alignment

- Hidden by default: catalogue, floating panel, canvas codes, profile note and tooltip — met by
  the new smoke and its renders.
- Layout editing: merge, Undo, hide and Save Extrusions with existing assignments kept — met by
  the new smoke; delete, track collapse, hide masks, direct interactions, topology persistence
  and scoped save are covered by their existing smokes, all passing.
- Cladding view unchanged — met by the new smoke and the full suite.
- Switch on restores assignment — met by the new smoke and `pc-editor-canvas-assignment`.
- Debug/Release builds and full regression — met.
- Live Rhino acceptance — pending; needs the build installed through the attested channel.

## Rollback

Set `_extrusionAssignmentEnabled` to `true` and rebuild, or revert this change. No Rhino
document, workbook, package, registry entry or installed RHP was changed.

## Remaining limitations

- Users cannot see or change assignments on panels that already carry them until the switch
  is restored; their data stays intact and still drives PCMatchCrv, PCSpawnCrv, PCSyncCrv and
  PCUpdate.
- The merge limitation above.
- No package was built and nothing was installed.

## Conclusion

Curve extrusion assignment is suspended in PCEditor and its Extrusion view edits the layout
only. Builds and the full regression pass. The work stays active until live acceptance in Rhino.

## Merge follow-up (2026-10-31)

At the user's request, `master` (PR 8 repository links, PR 10 command casing, PR 11 `PCbake`) was
merged into this branch before PR 9 was merged, and the package README paragraph above now names
`PCeditor`, `PCmatchCrv`, `PCsyncCrv` and `PCbake` (1.3.0 removed `PCspawnCrv` and renamed
`PCupdate`).

The new smoke closed the editor while it still had unsaved edits, so each run raised the real
"Discard unsaved panel cladding edits?" message box and waited for someone to answer it (Release
103.5 s in this TEST's evidence, 50.4 s in the combined run). It now clears the dirty state
before both `Close()` calls, as `pc-editor-canvas-assignment` does: 2.3 s Debug, 2.2 s Release.

`tests/Run-Regression.ps1` on the combined tree before that fix: Debug 79/79, Release 79/79, exit 0.
After it, `extrusion-assignment-suspension` passes in both configurations without a prompt.
