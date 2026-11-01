# Release 1.4.0 EXET

## Corresponding plan and execution date

[PLAN](../Project_Plan/261101_PLAN_release-1.4.0.md),
[TEST evidence](../Project_Test/261101_TEST_release-1.4.0/README.md).
Executed 2026-11-01 on branch `codex/release-1.4.0` from `master` (`97bb599`), worktree
`C:\01_Projects\Panel Cladding Full\PC-release-1.4.0`, host NOVAN-HOME. Package commit `8a9421b`.

## Changes

- `src/PanelCladdingEditor/PanelCladdingEditor.csproj`, `package-manifest.json`: version 1.4.0.
- `Packaging/PanelCladdingEditor/RELEASE-NOTES-1.4.0.md` (new): install, upgrade notes from 1.2.5
  (13 commands with `PCweb`), changes in 1.3.0 and 1.4.0, validation.
- Package README: the corrections from the 1.3.0 live run are marked 1.4.0.
- `tests/Live/PcLive.cs` and its README: the loaded-RHP check reads the expected version from the
  plug-in (or `Parameters.version`), `excluded` expects refreshed auto values on Not in Scope
  panels, `otherdocs` expects no issue for unmanaged objects.
- Plug-in code, product name, existing commands and GUID are unchanged.

## Deviations from plan

- None. The unreleased 1.3.1 preparation (`df86842` on `codex/release-1.3.1`, local only) was
  applied to this branch with 1.3.1 replaced by 1.4.0; `codex/release-1.3.1` is superseded and was
  never pushed.

## Discovered issues

None.

## Commands and results

See the [TEST README](../Project_Test/261101_TEST_release-1.4.0/README.md). In summary:
`Verify-Release.ps1` passed on `8a9421b` (release suites in Debug and Release, builds with zero
warnings, identity checks, package integrity, isolated install lifecycle, registration smoke); the
full regression passed 82/82 in Debug and Release on the same commit; the package RHP reports
`1.4.0+8a9421b`; the staged folder equals the verified bundle.

## Acceptance alignment

- Verify-Release on the committed source, clean tree, full regression: met.
- RHP version and GUID; installer registers 13 commands: met.
- Staged bundle equals the verified archive: met.
- Live acceptance after installation: pending (the user's `Install.cmd`).

## Rollback

Delete the branch, the generated package and the staged folder; 1.3.0 stays installed.

## Remaining limitations

- Not installed yet; production installation needs the user's `Install.cmd` and the Terminal-panel
  attestation in `AGENTS.md`.
- Not tagged, published, merged or built as a Yak package.

## Conclusion

PanelCladdingEditor 1.4.0 is built and verified on `8a9421b` and staged for installation.
