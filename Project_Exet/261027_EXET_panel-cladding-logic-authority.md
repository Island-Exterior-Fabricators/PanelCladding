# Panel cladding logic authority EXET

## Corresponding plan and execution date

Executed 2026-10-27 under
[the user-authorized PLAN](../Project_Plan/261027_PLAN_panel-cladding-logic-authority.md).

## Related artifacts

[TEST](../Project_Test/261027_TEST_panel-cladding-logic-authority/README.md) with the managed smoke
`PanelCladdingLogicAuthoritySmoke.csproj` and the fixture adapter `CladdingLogicFixture.cs`.
Branch `codex/panel-cladding-ud-keys`, on top of the uncommitted 1.2.2 (UD keys) and 1.2.3 (auto
attributes) work; no commit or PR was created.

## Implemented scope

- `PanelCladdingLogicService`: `Encode` stores a cell label as a parent reference and any other
  nonblank value, trimmed, as the cell's material (relaxed JSON escaping keeps codes readable).
  New `DecodeValues` returns label -> value; `Decode` returns the ownership graph with materials as
  roots, so `ResolveOwnerLabel` and graph validation are unchanged. `HasMaterials` recognizes
  ownership-only logic written before 1.2.4.
- `PanelCladdingKeyService`: `IsCladdingConfigured` (nonblank `UD_CLADDING_LOGIC` or legacy
  `CW_9.01`); `ReadCladdingCellValues` (cell values keyed by `CW_4` name from the logic; for
  ownership-only logic, each root cell's material from its own `CW_4` key). `Parse` takes cell values
  only from there and, when configured, requires the logic's cells to equal the grid's cells
  (`PANEL_CLADDING_LOGIC_GRID_MISMATCH`). `PanelCladdingKeySet.CladdingConfigured` added.
- `PCCreate` planning: no `CW_4` or logic writes; the reset pattern covers offset keys only, plus
  explicit retired keys (`CW_2.13_CLADDING_LOGIC`, retired cladding type keys). Stored logic that
  does not decode to exactly the new grid's labels is deleted (`UD_` and `CW_9.01`, any case).
- `PCMatchSrf`: an unconfigured source fails `PANEL_CLADDING_MATCH_SOURCE_NOT_CONFIGURED: ... no
  UD_CLADDING_LOGIC`. The source-`CW_4` "explicit cell" pass now uses the logic's values (every
  written or assigned cell takes its source value).
- `PCSyncCrv`: an unconfigured panel's plan has `WritesCladding = false` (new on the sync plan and
  panel write); the sync service skips its cell graph, the repository leaves its `CW_4` keys and
  logic alone, and the post-commit check skips the logic. The curves-scope grid rebuild reads cell
  values from the logic instead of `CW_4`. `PCSyncSrf` (surfaces scope) is unchanged and configures
  from cladding surfaces.
- Editor save: Save Extrusions alone that changes the H or V count of a configured panel fails
  `PANEL_CLADDING_LOGIC_REQUIRES_SAVE_BOTH`.
- Version 1.2.4 (`PanelCladdingEditor.csproj`, `package-manifest.json`); package README key table,
  `PCCreate` paragraph and a new *Cladding configuration (1.2.4)* section.

## Deviations from plan

- The frame configuration is still validated against H/V on unconfigured panels, as before. The
  request said an unconfigured panel depends only on H/V; `PCCreate` writes `UD_FRAME_CONFIG` from
  the same guides in the same step, so the two always agree after `PCCreate`. Relaxing the check
  was not done, to avoid silently dropping a saved frame configuration.
- 17 existing smokes encoded the old read path; see the test record. Their recorded results were
  not rewritten; each README gained a forward note.

## Discovered issues

- Logic written by 1.2.4 with no material at all (every cell blank) looks like pre-1.2.4 logic, so
  its root cells still take a material from their `CW_4` keys. Those keys are written blank by the
  same save, so this matters only if someone types a material into `CW_4` by hand on such a panel.

## Test record

- New smoke, Debug: `PASS: panel cladding logic authority regression` (6 `[OK]` groups).
- Full managed suite (78 projects, `dotnet run` per project, stdin empty, exit codes recorded):
  - Debug, first run on the final production code: 54/78. Besides the 7 known failures, 17 smokes
    failed because their fixtures store cladding only in `CW_4`, or assert the old format and
    `PCCreate` writes. Fixes: 13 route fixtures through `CladdingLogicFixture.WithLogic`
    (editor, match, spawn, match-parent-cells, match-topology-masks, layout-reconciliation,
    parent-fidelity, explicit-parent-cells, logical-cell-cleanup, repair-existing-targets,
    merge-mark-key, surface-coverage-metadata, layer-standard); create-command, curve-topology and
    logic-persistence assertions updated to the new rule; logical-cells gives its blank sources an
    all-blank logic, asserts an unconfigured source is refused, and expects
    `PANEL_CLADDING_LOGIC_PARENT_UNKNOWN` for its invalid reference. Debug rerun of those 17 plus
    the new smoke: 18/18 passed. The production code did not change after the first run.
  - Release, full run after the test fixes: 71/78. The 7 failures are the known pre-existing ones
    with the same messages (identity probe and native runtime probe need arguments, hide-smoke
    command list, type-code-format preview, pc-curve-display-colors 1.0.73, curve-release role CID,
    surface-release write check).
  - The runs rewrote 38 screenshots and added 2; all were restored/removed (`git status` shows no
    PNG changes).
- Production project rebuilt afterwards: Debug and Release succeeded, 0 warnings, 0 errors.
  `git diff --check`: clean.

## Installation (1.2.4, includes 1.2.2 and 1.2.3)

- Package: `Build-PanelCladdingEditorPackage.ps1 -Version 1.2.4`; identity probe passed (pluginId
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, one product); ZIP SHA-256
  `3d12f53aae9e03658191503cf3bb32ecaf24a58f7bacf52d13b8e1b9e2b853e7`; RHP 1.2.4.0, SHA-256
  `38FED9DA49749D57E79AF17BE0B7061B0876A92EC2C0B703BF119180174B62D4`. `Test-Package.ps1` passed
  (checksum, 32 payload hashes, compiled identity, isolated install/repair/validate/uninstall).
- Host access: the agent tool shell sees a private copy of HKCU (its `PlugIn` timestamp differed
  from the host's). The desktop app's Terminal panel runs outside that sandbox; a read from it before
  installing showed the host actually loading 1.0.96 (`PlugIn` 2026-10-08T00:26:39Z, 12 commands),
  not the 1.2.x recorded in earlier EXETs.
- Rhino closed; `Installer\Install-Package.ps1` run in a Terminal panel process: Install and
  Validate passed. 1.0.96 moved to rollback.
- Independent read-back (`PanelCladdingEditor-attest\Read-Registration.ps1`, separate host process,
  17:27:49Z): `PlugIn\FileName` = `…\plugin\1.2.4\PanelCladdingEditor.rhp`, hash as above, exactly
  13 commands (`2;<name>`), root/PlugIn/CommandList all 17:27:25Z.
- After the user started Rhino and opened a model (17:28:58Z): Rhino PID 46520 loaded exactly
  `…\plugin\1.2.4\PanelCladdingEditor.rhp` (no other PanelCladdingEditor module); root and
  CommandList advanced to 17:28:42Z, PlugIn unchanged at 17:27:25Z.

## Acceptance alignment

Managed criteria met. The live flow (TEST README) needs an installed 1.2.4 inside Rhino and was
not run.

## Rollback

Revert the branch. Panels saved by 1.2.4 carry materials in `UD_CLADDING_LOGIC`; 1.2.3 still loads
them from `CW_4` (which 1.2.4 keeps writing) but its `PCSyncSrf` rejects such logic as
`PANEL_CLADDING_LOGIC_PARENT_INVALID`.

## Remaining limitations

- Installed and attested on the host (see Installation); the live flow in the TEST record has not
  been run.
- A configured panel whose logic no longer fits its offsets (for example after hand-editing
  `CW_2.03`/`CW_2.04`) now fails to load until `PCCreate` is re-run or the logic is cleared;
  before 1.2.4 it loaded from `CW_4`.
- Hand edits to `CW_4` keys no longer change what the plug-in shows; the next cladding save
  overwrites them from the logic.
- Panels with ownership-only logic keep reading their `CW_4` materials until a cladding write
  stores them in the logic (no model-wide pass, PLAN future extension).

## Conclusion

1.2.4 reads cladding only from `UD_CLADDING_LOGIC`, which now carries materials; `CW_4.xx` is
output only. Unconfigured panels load from the grid and are not given cladding keys by `PCCreate` or
`PCSyncCrv`. Builds pass and the managed suite matches the baseline apart from the 17 smokes updated
for the new rule. 1.2.4 is installed and attested on the host and loaded by Rhino.
