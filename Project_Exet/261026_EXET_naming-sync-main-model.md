# Naming-convention sync from the main model only EXET

## Corresponding plan and execution date

Executed 2026-10-26 under
[the user-authorized PLAN](../Project_Plan/261026_PLAN_naming-sync-main-model.md).

## Related artifacts

[Scoped TEST](../Project_Test/261026_TEST_naming-sync-main-model/README.md); the check is
`VerifyMainModel` in
[the naming-convention smoke](../Project_Test/261023_TEST_panel-cladding-naming-convention/README.md).
Same working branch `codex/panel-cladding-layer-standard`, on top of the uncommitted 1.0.98–1.2.0
changes; no commit or PR was requested or created.

## Implemented scope

- `PanelCladdingProjectService.IsMainModelPath(path)`: true only when the extension is `.3dm` and
  the file name without extension equals `<code> - Wireframe` ignoring case, `<code>` being
  `CodeFromDocumentPath(path)`. `MainModelNameSuffix` = `" - Wireframe"`;
  `MainModelFileName(code)` gives `<CODE> - Wireframe.3dm` for messages.
- `LivePanelNamingConventionSync.Sync`: unchanged up to and including the sheet read, format
  refresh, model scan, mismatch report and reservation of the sheet's Frame-Config rows. A model
  that is not the main model then returns before the Job Code check, table build and write, and
  prints once per document session
  `<sheet> is updated only when <CODE> - Wireframe.3dm is saved; <file> only reads it.`
  The flag is cleared when the document closes.
- `PCtemplatePNL` unchanged (explicit command, any saved model of the project).
- Package README: the save-time sync paragraph names the main model and what other models do
  (since 1.2.1).
- Version 1.2.1 (`PanelCladdingEditor.csproj`, `package-manifest.json`); the command list is
  unchanged (13 commands).

## Deviations from plan

The PLAN excluded packaging. After the build the user asked for the bump to 1.2.1, the full managed
suite and a staged package, without installation; those are recorded below.

## Test record

- `dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj` Debug and Release: succeeded,
  0 warnings, 0 errors.
- Naming-convention smoke, Debug and Release: all checks pass, including the new
  `[OK] only <PROJECT> - Wireframe.3dm syncs the sheet; XYZ - Wireframe_STN.3dm and copies do not`.
- Layer-standard smoke (owns `CodeFromDocumentPath`), Debug and Release: pass.
- `git diff --check` on the changed source and README: clean.
- Full managed suite at 1.2.1 (75 projects, `dotnet run` per project, stdin empty, exit codes
  recorded): Debug 68/75, Release 68/75. The 7 failures are the known pre-existing ones:
  `PluginAssemblyIdentityProbe` and `NativeRuntimeProbe` (need arguments), hide-smoke-command
  (old command list), type-code-format (editor preview), pc-curve-display-colors ("must ship as
  package 1.0.73"), curve-release ("Role CID must remain unchanged") and surface-release ("Surface
  release-only changes must not trigger other writes"; the 1.2.0 EXET listed it under the
  curve-release message, but this smoke has no such check and does not use the naming-convention
  or project services). The suite rewrote 36 screenshots and added 2 under `Project_Test`; all were
  restored from a snapshot taken before the run (0 differences afterwards).

## Package (staged, not installed)

- `Build-PanelCladdingEditorPackage.ps1 -Version 1.2.1`: publish and direct RHP rebuild succeeded;
  the identity probe passed (pluginId `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, one product).
- `PanelCladdingEditor-1.2.1-win-x64.zip` (15,970,426 bytes), SHA-256
  `d58f170e12935a23280f4f64641809f28aaa1f22bc2b180c64c9322e207d72a5`; RHP SHA-256
  `d4203d7c5f74b7d329397ef13978d282bc2578a43fe57bd97f354da1fc1b5fde`, assembly 1.2.1.0; the RHP
  metadata contains `IsMainModelPath`.
- `Test-Package.ps1`: checksum, all 32 payload hashes, Windows x64 dependencies, compiled identity
  of the bundle RHP, and the isolated non-admin install/repair/validate/uninstall
  (`HKCU\Software\PanelCladdingEditor\ReleaseTests\<id>`, `%LOCALAPPDATA%\PanelCladdingEditor-ReleaseTests`)
  passed.
- Staged at `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.2.1`; 32 files, 0 hash
  mismatches against `bundle-manifest.json`.
- Not done: installation, registry attestation and the live Rhino check in the TEST record. The
  installer must be run by the user (the agent shell's HKCU writes are virtualized, see the 1.2.0
  EXET), followed by `PanelCladdingEditor-attest\Read-Registration.cmd`.

## Acceptance alignment

Main-model names are accepted and secondary models, copies, versions, other extensions, autosave
and code-less names rejected (managed). Builds pass. The live acceptance (STN save leaves the sheet
unchanged and prints the note once; main-model save updates it) needs an installed package with
this change and runs inside Rhino.

## Rollback

Revert `PanelCladdingProjectService.cs`, `LivePanelNamingConventionSync.cs` and the version. No
sheet or model was touched, and 1.2.0 stays installed.

## Remaining limitations

- The installed 1.2.0 plug-in still syncs from every model until the staged 1.2.1 is installed and
  attested (AGENTS.md).
- With only the main model writing, the struck state and new wall/frame-config rows follow the main
  model alone. Codes a secondary model assigns for configurations the main model does not have
  reach the sheet only when the main model gets them. The 1.2.0 EXET noted that
  `XYZ - Wireframe.3dm` holds wall types such as `3A.5` that the sheet does not list; its next save
  will add them and set the struck state from its usage.
- A project whose main model does not follow `<PROJECT> - Wireframe.3dm` no longer syncs.

## Conclusion

The save-time naming-convention sync now writes the sheet only from `<PROJECT> - Wireframe.3dm`.
1.2.1 builds, passes the managed suite apart from the known pre-existing failures, passes the
package test and is staged; it is not installed.
