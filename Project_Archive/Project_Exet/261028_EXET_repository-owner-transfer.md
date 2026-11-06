# Repository owner transfer EXET

## Corresponding plan and execution date

[PLAN](../Project_Plan/261028_PLAN_repository-owner-transfer.md),
[TEST evidence](../Project_Test/261028_TEST_repository-owner-transfer/README.md).
Executed 2026-10-28 on branch `codex/repository-owner-transfer` from `master` (`ad23285`), host
NYCZBF16G9NXU; commit `b48611c`,
[PR 8](https://github.com/nuofan-island/PanelCladding/pull/8).

## Changes

- GitHub: at the user's request the transfer of `novan-xu/PanelCladding` to `nuofan-island` was
  requested through the REST transfer endpoint as `novan-xu`, and accepted from the
  `nuofan-island` account. Releases, tags and branches moved; the old URL redirects.
- Local `origin` now points at `https://github.com/nuofan-island/PanelCladding.git`.
- `README.md`: the MCP_Rhino link and the latest-release link name `nuofan-island`.
- `Packaging/PanelCladdingEditor/README.md` and `QUICKSTART.txt`: the release links name
  `nuofan-island`; both ship in the next bundle.
- `Packaging/PanelCladdingEditor/Build-PanelCladdingEditorYak.ps1`: the Yak manifest `url` is
  `https://github.com/nuofan-island/PanelCladding`.
- Plug-in code, version, product name, commands and GUID are unchanged.

## Deviations from plan

- The four link edits were made in the same session shortly before the PLAN was written; the PLAN
  precedes all builds, tests and the commit.

## Discovered issues

- `MCP_Rhino` had also moved to `nuofan-island`, so the README origin link was updated as well.
- Running `Build-PanelCladdingEditorYak.ps1` with an all-stream redirect (`*>&1`) in Windows
  PowerShell 5.1 aborts on Yak's content-version warning. A plain call works; the script is
  unchanged because the redirect is not part of its documented use.
- Published release assets (`v1.0.97`, `v1.2.5`) keep their `novan-xu` links inside the bundled
  README and QUICKSTART; GitHub redirects them, and the next package carries the new links.

## Commands and results

See the [TEST README](../Project_Test/261028_TEST_repository-owner-transfer/README.md). In summary:
the repository and releases resolve under `nuofan-island`; `git grep novan-xu` finds no current
reference; the full regression passed 77/77 in Debug and in Release; a scratch package build
passed `Test-Package.ps1` and `Verify-YakPackage.ps1` and carries the new links.

## Acceptance alignment

All PLAN acceptance items are met. Live Rhino acceptance was not rerun, as planned.

## Rollback

Revert the commit and set `origin` back to `https://github.com/novan-xu/PanelCladding.git`.
Transferring the repository back is a separate GitHub action.

## Remaining limitations

- The repository is public under a user account, not a company organization.
- PLAN / EXET / TEST and archived records keep their `novan-xu` links as historical evidence.

## Conclusion

The repository is owned by `nuofan-island`, the local clone pushes there, and every current link
and the Yak manifest name the new owner.
