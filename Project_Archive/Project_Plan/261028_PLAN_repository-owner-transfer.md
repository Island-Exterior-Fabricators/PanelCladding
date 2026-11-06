# Repository owner transfer to nuofan-island

Execution date: 2026-10-28 (America/New_York).

## Background

The repository moved from the personal account `novan-xu` to the company account
`nuofan-island` on 2026-10-28 through GitHub's ownership transfer, so issues, PRs, releases
(`v1.0.97`, `v1.2.5`) and tags moved with it and the old URLs redirect. `MCP_Rhino` was moved to
the same account. The local `origin` and several current files still name `novan-xu`.

## Goals

- Point the local `origin` remote at `https://github.com/nuofan-island/PanelCladding.git`.
- Replace the `novan-xu` GitHub URLs in current, user-facing files so new downloads, the next
  package bundle and the Yak manifest name the new owner without relying on redirects.
- Change nothing else: plug-in code, version, product name, commands and GUID
  7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35 stay as they are.

## Architecture ownership

Repository identity only. The architecture guide already keeps product identity separate from the
repository name; no build, install or registration behaviour depends on the repository URL.

## Design

- Update these links:
  - `README.md`: the MCP_Rhino origin link and the latest-release download link.
  - `Packaging/PanelCladdingEditor/README.md`: the downloadable-release link (ships in the bundle).
  - `Packaging/PanelCladdingEditor/QUICKSTART.txt`: the releases link (ships in the bundle).
  - `Packaging/PanelCladdingEditor/Build-PanelCladdingEditorYak.ps1`: the manifest `url` field.
- Leave PLAN / EXET / TEST records, archived records and `RELEASE-NOTES-*.md` unchanged. They are
  historical evidence of where work was executed and published, and GitHub redirects their links.
- Already published release assets are immutable and keep their old links; the next package
  built from this source carries the new ones. No version bump is needed for a link-only change.

## Affected files

The four files above and the matching PLAN / EXET / TEST records.

## Usage

None for users. Developers with an older clone run
`git remote set-url origin https://github.com/nuofan-island/PanelCladding.git`.

## Acceptance

- `git remote -v` names `nuofan-island/PanelCladding` and `git fetch` succeeds.
- `git grep novan-xu` finds no match outside the PLAN / EXET / TEST and archive folders.
- The full regression suite passes in Debug and Release.
- A locally built package bundle contains the new links in `README.md` and `QUICKSTART.txt`, and a
  locally built `.yak` has the new manifest `url` and passes `Verify-YakPackage.ps1`. The package
  is built only; nothing is installed or published.
- Live Rhino acceptance is not affected by this change and is not rerun.

## Rollback

Revert the commit and set `origin` back to the old URL; GitHub redirects either way. Transferring
the repository back to `novan-xu` is a separate GitHub action outside this change.

## Future extensions

If the company later creates a GitHub organization, transfer again and repeat this link update.
