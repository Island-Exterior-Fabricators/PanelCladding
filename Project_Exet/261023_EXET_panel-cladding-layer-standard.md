# Panel cladding project layer standard EXET

## Corresponding plan and execution date

Executed 2026-10-23 under
[the user-authorized PLAN](../Project_Plan/261023_PLAN_panel-cladding-layer-standard.md).

## Related artifacts

[Scoped TEST code, fixture and commands](../Project_Test/261023_TEST_panel-cladding-layer-standard/README.md).
Branch `codex/panel-cladding-layer-standard` from `master` `91f50ed`. No commit or PR
was requested or created.

## Implemented scope

| Objects | Retired | Now |
| --- | --- | --- |
| Source panels | `01_CW Panels::Surfaces-PNL` subtree | `<PROJECT>-IEF-Wireframe::Surfaces - PNL` subtree |
| Extrusion curves | `02_CW Extrusions::Curves-PNL::<suffix>` | `<same root>::Curves - PNL::Curves - <type>` |
| Cladding surfaces | `04_STEP Surfaces::Surfaces-Glass::GL01` (+ legacy `03_Material Surfaces (STEP)` read) | `Reference::Surfaces - STEP::Surfaces - <prefix>::<material>` |

- `PanelCladdingExtrusionPlanningService` owns the project root (`*-IEF-Wireframe`,
  non-blank code, case-insensitive), the panel/curve branch predicates and curve
  routing. Each `Surfaces - ` type segment becomes `Curves - `; unprefixed segments
  are kept; the panel's own root name is reused. Failures name the expected pattern.
- `PanelCladdingSpawnPlanningService`: STEP root is `Reference::Surfaces - STEP`;
  the family layer is `Surfaces - ` + the code's leading letters (`Surfaces - Other`
  when there are none); `BuildMaterialLayerPath` is the single path builder. The
  former family table is now `ResolveMaterialCategory`, still driving default
  colours and workbook catalogue categories. Managed curves are the curve branch.
- `PanelCladdingSurfaceSyncPlanningService` reads materials only from
  `Reference::Surfaces - STEP::<family>::<material>` and keeps the family check.
- PCpid scope reuses the panel predicate; its prompt and messages show
  `<PROJECT>-IEF-Wireframe::Surfaces - PNL`.
- Version 1.0.98 (csproj, package manifest); package README documents the standard.

Live Rhino adapters were not changed beyond PCpid messages: spawn, update, sync and
PCpid already route every layer decision through these planning predicates.

## Deviations from plan

None in behavior. Seven UI/editor fixtures with placeholder `01_CW Panels::Surface`
or `01_CW Panels::WT-01` layer strings were left unchanged; they never were valid
panel paths and are not used for layer decisions.

## Problems found and resolved

- Running the full smoke suite regenerates 34 tracked screenshot PNGs in historical
  UI TEST folders and adds two untracked ones. They were restored with
  `git checkout` / removed after every run so historical evidence is unchanged.
- The first runs passed `--nologo` after `dotnet run`, which reached the programs as
  an argument; several smokes used it as an output folder and created `./--nologo/`.
  That folder was deleted and later runs omit the argument. The baseline run on
  `master` also failed five projects only because its worktree path exceeded the
  Windows command-line length; those pass from the repository path.

## Test record

Baseline (`master`, separate worktree): 60/72 smoke projects passed.

Branch before fixture moves: 15 previously passing projects failed, each on the
retired layer paths (spawn, surface-sync, offset-sync, extrusion-sync,
curve-topology, hide-mask, layout-reconciliation, scoped-save, split-spawn-sync,
sparse-topology, managed-cid-scope, pcspawncrv, role-cid, object-names,
pid-layer-scope). Their fixtures/assertions were moved to the new tree; the
semantic tests (managed-cid-scope, pcspawncrv, pid-layer-scope, offset-sync,
extrusion-sync) now assert the new scopes and rejection of the retired trees.
PCpid/point-order native fixtures create `ABC-IEF-Wireframe::Surfaces - PNL`.

Final, after all source changes:

- Debug, all 73 projects: 66 pass. The 7 failures also fail on `master`, for
  unrelated reasons: `PluginAssemblyIdentityProbe` and `PanelExtrusionNativeRuntimeProbe`
  require arguments; hide-smoke-command (Rhino command list); type-code-format
  (editor preview shape); pc-curve-display-colors ("must ship as package 1.0.73");
  curve-release and surface-release (release-only change detection).
- Release, 21 layer-related projects including the new smoke: 21/21 pass.
- `261023_TEST_panel-cladding-layer-standard`: all nine checks pass in Debug and Release.
- `Verify-Regression.ps1` (pid-planar-bounds): five PCpid smokes, solution build and
  standalone RHP build pass in Debug and Release.
- `Verify-PluginAssemblyIdentity.ps1 -Configuration Release`: plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, assembly/class/manifest GUID and 1.0.98
  version agree.
- `git diff --check` passes.

Release RHP SHA-256 (not packaged or installed):
`0a6a60ea8b1fa213bed3b3d385a9788effe0b6e14533da7d50d829fbf4192673`.

The live `ABC - Wireframe.3dm` was only read (four read-only C# surveys through the
selected Router route; Rhino logged an Undo record per script with no changes).
No document was modified or saved.

## Acceptance alignment

- All six live ABC panel→curve pairs (849 PIDs) and all fifteen STEP material
  layers are reproduced exactly by the planners. A live WT_11 panel (E2_03_02)
  plans `Surfaces - MPL::MPL-001`, `Surfaces - LVR::LVR-001` and `Curves - WT_11`.
- Scopes over the ABC layer list are exactly 9 panel, 18 curve and 21 STEP layers;
  another project code (`QRS`) behaves identically.
- The 1,861 wireframe `Surfaces - GLS` surfaces, which carry the same kind of
  PID/CID/`Cladding` metadata as STEP output (per-material counts nearly match), are
  outside managed scope, so spawn/update/sync do not treat them as dependencies.
- Retired trees fail closed with the expected pattern. Colours and workbook categories
  are unchanged. `src/` contains none of the retired layer names.
- Live command acceptance in Rhino is pending installation of 1.0.98.

## Rollback verification

The installed 1.0.97 RHP and its registration were not touched. Source rollback is
reverting this branch. No document data was migrated.

## Remaining items and observations

- `Surfaces - Not in Scope` is inside `Surfaces - PNL`, so it remains part of the
  PCpid numbering context and its panels would route curves to
  `Curves - Not in Scope`. Excluding it would be a separate, explicit rule.
- Observed, unchanged: 763 of 1,099 ABC panel PIDs also appear on 2,421 wireframe
  objects outside the managed trees (`Surfaces - GLS` 1,832, `Surfaces - MP` 326,
  `Surfaces - STN` 263). PCpid treats such unmanaged PID holders as collisions, so
  re-running PCpid on those panels is refused. This is existing PCpid policy.
- Documents still on the retired tree must be re-layered before use with 1.0.98.
- Packaging, installation (Rhino closed, host attestation, `-Mode Validate`) and
  native PCSpawnCrv/PCSpawnSrf/PCUpdate/PCSyncSrf/PCSyncCrv/PCpid acceptance in ABC.

## Conclusion

The plug-in now follows the project layer standard for panels, extrusion curves and
STEP cladding surfaces, verified against the live ABC layer data and the managed
regression suite. Packaging, installation and in-Rhino command acceptance are not
claimed.

## Packaging and installation follow-up — 2026-10-23

`Build-PanelCladdingEditorPackage.ps1` produced
`PanelCladdingEditor-1.0.98-win-x64.zip` (SHA-256
`41dfafe8b08a8f374e5c56411c9673be9337c6c46e530b990da2ef2677fb86b1`).
`261014_TEST_first-package-release/Test-Package.ps1` passed: ZIP checksum, all 32
payload hashes, compiled identity, and the isolated install/repair/validate/uninstall
lifecycle. The bundle was staged at `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.0.98`;
its RHP equals the tested `0a6a60ea…2673`.

A host write probe from the agent session (HKCU nonce, read back through the WMI
StdRegProv `HKEY_USERS\<SID>` view) returned "not found", so session registry
writes still do not persist to the host hive; the agent did not run Install.
The first attempt was also blocked by a terminated `Rhino.exe` (PID 13712, zero
threads) still referenced by another process, which the installer's Rhino check
counts as running; ending the MCP_Rhino routers, DeepSeek Harness and Codex helpers
did not release it, and a Windows restart did.

The user then ran `Install.cmd`. Independent read-only checks after it exited:

- StdRegProv `PlugIn\FileName` = `%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.98\PanelCladdingEditor.rhp`;
  the file exists with SHA-256 `0a6a60ea…2673`.
- `CommandList` contains exactly the 12 commands, each `2;<name>`.
- Root, `PlugIn` and `CommandList` write times all advanced past the pre-install
  snapshot (root/PlugIn 19:17:17.43Z, CommandList 19:17:17.44Z).
- `install-manifest.json` reports 1.0.98; the prior 1.0.97 RHP rollback copy hash matches.
- `Install-PanelCladdingEditor.ps1 -Mode Validate` from the staged bundle passed (exit 0).
- No Rhino process was running.

Post-start: after Rhino next started, root and `CommandList` advanced to 19:57:25.73Z
while `PlugIn` stayed at the install time (19:17:17.43Z) and `FileName` still names the
1.0.98 RHP. The loaded module itself was not inspected. Native command acceptance was
superseded by the 1.0.99 ID change.
