# Panel cladding PID/CID separator EXET

## Corresponding plan and execution date

Executed 2026-10-23 under
[the user-authorized PLAN](../Project_Plan/261023_PLAN_panel-cladding-id-separators.md).

## Related artifacts

[Scoped TEST](../Project_Test/261023_TEST_panel-cladding-id-separators/README.md).
Same working branch `codex/panel-cladding-layer-standard`, on top of the uncommitted
layer-standard change; no commit or PR was requested or created.

## Implemented scope

| ID | Before | Now |
| --- | --- | --- |
| PID | `PID_ABC_N2_05_05` | `PID-ABC-N2-05-05` |
| Panel CID | `CID_ABC_N2_05_05-C` | `CID-ABC-N2-05-05_C` |
| Surface CID | `CID_ABC_N2_05_05-C-0A` | `CID-ABC-N2-05-05_C_0A` |
| Curve CID / CRV | `CID_ABC_N2_05_05-C-FRM_0` / `FRM_0` | `CID-ABC-N2-05-05_C_FRM-0` / `FRM-0` |
| Name | `N2_05_05-C` | `N2-05-05_C` |

- `PanelCladdingCidService` defines `IdSeparator` (`-`), `ComponentSeparator` (`_`),
  `PID-`/`CID-` prefixes, `_P`/`_C` roles and `Component(cid, part)`. ShortName strips
  `CID-ABC-` or `CID-`. Dependency ownership uses `_P_`/`_C_`.
- PCpid builds `PID-<code>-<elevation>-<level>-<bay>`; its dependency check uses `cid_`.
- Spawn, extrusion and both sync paths join with `Component`.
- Curve codes `FRM-0..3`, `INT-<track><bay>`, `INT-<track>`, `INT-<track><start>–<end>` in
  extrusion planning, live geometry partitioning, the editor extrusion model and
  editor frame-key checks. `CW_1.09_FRAME_TYPE` frame keys must match `^FRM-[0-3]$`.
- Version 1.0.99; package README examples and a 1.0.99 note.

## Deviations from plan

None. Six ABC corner panels whose names do not equal their old CID-derived short name
(e.g. name `N1_03_01`, CID `…E1_03_04-C`) kept their names unchanged.

## Document migration (ABC - Wireframe.3dm, live route, Rhino PID 9912)

Read-only survey before migration: 44,637 objects; 20,845 with PID, 15,407 with CID,
7,060 with `CRV`; 13,263 names equal the old short name. No other user-text key, block
definition, legacy `CW_2.09_FRAME_ASSIGNMENTS` or `CW_1.09_FRAME_TYPE` frame key (61
payloads, none with perimeter keys) contained the old forms. All values were old format.

The migration swapped `_`↔`-` in `CW_1.01_PID`, `CW_1.02_CID` and `CRV` and renamed
objects whose name equalled the old short name, in one Undo record. Dry run, then apply:
20,845 objects, 43,312 value writes, 13,263 renames, 0 failures. The 2,099 CID values
shared by more than one object (mainly wireframe glass copies of STEP surfaces) kept
identical duplicate structure.

Independent read-back against the 1.0.99 rules: 0 old-format PID/CID/CRV values;
7,060/7,060 curve CIDs = panel base + optional `_P`/`_C` + `_` + CRV; 5,004/5,004 STEP
surface CIDs = panel base + optional role + `_<cell>`; 13,263/13,269 named objects match
their CID (the 6 deliberate exceptions above). 1,133/1,143 panel CIDs equal
`CID-` + PID + role. The other 10 already disagreed before the swap and were not
changed: on `Surfaces - WT_08 (SB)`, two panels have PID `W9-…` but CID `S11-…` and four
PIDs end in `'`; on `Surfaces - WT_04`, four PF-level corner panels have CIDs without the
role suffix or pointing at another panel. The document was not saved by the agent.

## Test record

- Pre-existing failures are unchanged: PluginAssemblyIdentityProbe, NativeRuntimeProbe,
  hide-smoke-command, type-code-format, pc-curve-display-colors, curve-release,
  surface-release (same messages as on `master`). Their ID literals were also moved,
  but their first failing checks precede any ID assertion, so those literals are unverified.
- After the source change, 17 further projects failed on old-format literals. Literals
  in 39 test files were converted by rule (PID/CID tokens, component-join fragments,
  short names, curve codes; error codes, `CW_` keys and profile codes excluded), every
  changed line was reviewed, and three role-cid/surface-sync joins plus the padded
  spawn PID were fixed by hand.
- Final: Debug 67/74 and Release 67/74 smoke projects pass (the 7 above fail).
  `261023_TEST_panel-cladding-id-separators` passes all five checks.
  `Verify-Regression.ps1` passes Debug/Release PCpid smokes, solution and RHP builds.
  Release identity check passes (GUID, 1.0.99).
- Package `PanelCladdingEditor-1.0.99-win-x64.zip` (SHA-256
  `4ddda92bf123f5c64b68a7830e74230a73a3128fde08b4d6d5394858eeaca0ed`) passed
  `Test-Package.ps1` (32 payload hashes, identity, isolated lifecycle). Staged at
  `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.0.99`; RHP SHA-256
  `07b184f1c800796b96980804cde19c2838a62637950f494ac7f283f82970dd9a`.
- Regenerated historical screenshots were restored after each run.

## Acceptance alignment

All ID kinds follow the new rule in code and in the migrated ABC document; the user's
example `CID_ABC_N2_05_05-C` → `CID-ABC-N2-05-05_C` is asserted directly.

## Rollback verification

Before saving, Rhino Undo reverts the migration in one step. Source rollback is
reverting the branch. 1.0.98 remains installed until the user installs 1.0.99; it must
not be used on the migrated document.

## Remaining items

User: save ABC, close Rhino, run the staged `Install.cmd`; then independent registry
read-back and installer Validate. Native in-Rhino command acceptance on ABC with 1.0.99.

## Follow-up corrections and installation — 2026-10-23

After the migration the user corrected the 10 pre-existing panels in Rhino. A
re-audit found four new duplicate PID/CID pairs on `Surfaces - WT_08 (SB)`: vertical
and horizontal panels in the same bays once the distinguishing `'` was removed. The
user set the five horizontal panels' elevation to `S12`; their PID/CID/name were then
rewritten to `PID-ABC-S12-03-01..05` / `CID-ABC-S12-03-0x` / `S12-03-0x` (no collisions,
no dependencies). On the user's request, 12 panel names were set to their CID short
name. Final audit: 0 old-format values, 0 duplicate CIDs per family, 0 PID/CID
mismatches, 0 orphan or mismatched STEP surfaces/curves, all names match CIDs.
105 panels still have an unsupported unit type (96 `Not in Scope`, 9 WT_04 `TBD`).
The user saved ABC at 21:12:17Z, after these edits.

The first reported install had not run (registry and files unchanged). After the user
ran the staged `Install.cmd` again, independent checks: StdRegProv `PlugIn\FileName` =
`%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.99\PanelCladdingEditor.rhp` with the tested
SHA-256 `07b184f1…dd9a`; exact 12-command `CommandList`; root, `PlugIn` and `CommandList`
times all advanced past the pre-install snapshot; `install-manifest.json` = 1.0.99;
the 1.0.98 rollback copy hash matches; `-Mode Validate` passed; no Rhino process.

## Conclusion

The plug-in now produces and matches `-`-separated IDs joined by `_`, and ABC has been
converted in memory and verified; saving and installing 1.0.99 are pending.

## GitHub publication follow-up (2026-10-27)

Source commit: `971f652e7565c083af77ce55dac70001e3f2b5cf`.
Pull request: [#2 — project layer standard, ID separators and PNL naming convention](https://github.com/novan-xu/PanelCladding/pull/2).
