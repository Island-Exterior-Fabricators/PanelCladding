# Repository transfer to Island-Exterior-Fabricators

Execution date: 2026-11-04 (America/New_York).

## Background

At the user's request, `PanelCladding` and `MCP_Rhino` move from the account `nuofan-island` to
the company organization `Island-Exterior-Fabricators` (display name "Island Exterior
Fabricators") through GitHub's repository transfer. `wireframe-web`, which PCweb links to, already
lives in that organization; `nuofan-island/wireframe-web` only redirects. The 2026-10-28 transfer
PLAN listed this move as its future extension. The local `origin`, several current files and the
PCweb download URL constant still name `nuofan-island`.

## Goals

- Transfer `nuofan-island/PanelCladding` (stays public) and `nuofan-island/MCP_Rhino` (stays
  private) to `Island-Exterior-Fabricators`, keeping issues, PRs, releases and tags.
- Point the local `origin` at `https://github.com/Island-Exterior-Fabricators/PanelCladding.git`.
- Replace the `nuofan-island` GitHub URLs in current files, so new downloads, the next package
  bundle, the Yak manifest and PCweb's download hint name the organization without redirects.
- Change nothing else: version, product name, commands and GUID
  7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35 stay as they are.

## Architecture ownership

Repository identity only. The one source change is the `WireframeWebLauncher.DownloadPage`
constant, a URL shown to the user when Wireframe Web is missing; launch logic is unchanged.

## Design

- Update these links:
  - `README.md`: the MCP_Rhino origin link and the latest-release download link.
  - `Packaging/PanelCladdingEditor/README.md`: the wireframe-web releases link and the
    downloadable-release link (both ship in the bundle).
  - `Packaging/PanelCladdingEditor/QUICKSTART.txt`: the releases link (ships in the bundle).
  - `Packaging/PanelCladdingEditor/Build-PanelCladdingEditorYak.ps1`: the manifest `url` field.
  - `src/PanelCladdingEditor/Infrastructure/WireframeWeb/WireframeWebLauncher.cs`: `DownloadPage`.
- Leave PLAN / EXET / TEST records, archived records and `RELEASE-NOTES-*.md` unchanged as
  historical evidence; GitHub redirects their links.
- Published release assets keep their old links. No version bump for a link-only change; the next
  release carries the new links.

## Affected files

The five files above and the matching PLAN / EXET / TEST records.

## Usage

None for users. Developers with an older clone run
`git remote set-url origin https://github.com/Island-Exterior-Fabricators/PanelCladding.git`.

## Acceptance

- Both repositories resolve under `Island-Exterior-Fabricators` with unchanged visibility, and the
  PanelCladding releases are listed there.
- `git remote -v` names the organization and `git fetch` succeeds.
- `git grep nuofan-island` finds no match outside the PLAN / EXET / TEST and archive folders.
- Debug and Release builds and the full regression suite pass.
- A locally built package bundle and `.yak` carry the new links and pass `Test-Package.ps1` and
  `Verify-YakPackage.ps1`. Nothing is installed or published.
- Live Rhino acceptance is not affected and is not rerun.

## Rollback

Revert the commit and set `origin` back to `https://github.com/nuofan-island/PanelCladding.git`.
Transferring the repositories back is a separate GitHub action by an organization owner.

## Future extensions

None planned.
