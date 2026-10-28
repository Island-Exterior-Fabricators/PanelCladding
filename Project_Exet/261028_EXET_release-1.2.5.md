# Release 1.2.5 EXET

## Corresponding plan and execution date

[PLAN](../Project_Plan/261028_PLAN_release-1.2.5.md),
[TEST evidence](../Project_Test/261028_TEST_release-1.2.5/README.md).
Executed 2026-10-28 on branch `codex/release-1.2.5` from `master` (`09365d8`), host NOVAN-HOME.
Release: [PanelCladdingEditor v1.2.5](https://github.com/novan-xu/PanelCladding/releases/tag/v1.2.5).

## Changes

- `README.md`: the install section downloads `PanelCladdingEditor-1.2.5-win-x64.zip`, says how to
  upgrade an earlier zip installation, and links the release notes.
- `Packaging/PanelCladdingEditor/RELEASE-NOTES-1.2.5.md` (new): install steps, the migration
  notes for documents made with 1.0.97 (layer standard, ID separators, unit types, project code,
  `UD_` attributes, refresh on open), changes since 1.0.97, and the validation scope.
- `Packaging/PanelCladdingEditor/README.md`: the installer writes a thirteen-command
  `CommandList`, not twelve (PCtemplatePNL since 1.2.0).
- Commit `9f48c11` holds these changes and is tagged `v1.2.5` (annotated). The release carries the
  zip and its `.sha256` sidecar built and verified on that commit, as for `v1.0.97`.
- Plug-in code, version, product name, commands and GUID are unchanged.

## Deviations from plan

- None in scope. The tag is on the branch commit that built the package, not on the merge commit
  as for `v1.0.97`, because the RHP's product version embeds its source commit
  (`1.2.5+<commit>`); the PLAN records this choice.
- The release was published before the PR merge so the records could cite the published digests;
  the tagged commit enters `master` with the merge.

## Discovered issues

- The 1.2.5 package staged for installation earlier the same day (built at `4d73f0e`) has a
  different RHP hash from the released one only because of the embedded commit; its code is the
  same. The staged folder `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.2.5` was
  re-extracted from the released zip (33 files identical to the verified bundle, RHP
  `1.2.5+9f48c11`), so the pending installation uses the published package.
- The package README still said twelve commands; corrected before the verified build.

## Commands and results

See the [TEST README](../Project_Test/261028_TEST_release-1.2.5/README.md). In summary:
`Verify-Release.ps1` passed on `9f48c11` (release suites in Debug and Release, builds with zero
warnings, identity checks, package integrity, isolated install lifecycle, registration smoke).
GitHub reports the asset digests equal to the verified local files, and `releases/latest` is
`v1.2.5`.

## Acceptance alignment

All PLAN acceptance items are met.

## Rollback

Keep the published assets; correct problems with a new version. Reverting the README commit
restores the 1.0.97 instructions but leaves the release in place.

## Remaining limitations

- Live in-Rhino acceptance of most changes since 1.0.97 is still pending in their own records
  (1.0.99 to 1.2.5); the release notes say so.
- 1.2.5 is not installed on NOVAN-HOME yet (1.2.0 is); production installation still needs the
  host attestation in `AGENTS.md`.
- The Yak package was not built or published.

## Conclusion

PanelCladdingEditor 1.2.5 is published on GitHub as the latest release, with assets identical to
the package verified on the tagged commit, and the README points to it.
