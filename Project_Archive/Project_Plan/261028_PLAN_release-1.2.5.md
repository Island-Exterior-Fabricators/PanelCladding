# Release 1.2.5 on GitHub

Execution date: 2026-10-28 (America/New_York).

## Background and goals

GitHub Releases still offers only v1.0.97 (2026-10-14). Versions 1.0.98 to 1.2.5 were packaged
and installed locally but never published, and the root README still links the 1.0.97 zip.
Publish the current 1.2.5 package as release `v1.2.5` with its zip and SHA-256 sidecar, and point
the README download instructions at it. Plug-in code, product name, commands and GUID
7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35 are unchanged.

## Ownership and design

- Follow the v1.0.97 procedure (`261014_PLAN_first-package-release`): run
  `tests/Package/Verify-Release.ps1` on committed source (release smokes in Debug and Release,
  solution and direct RHP rebuilds, compiled identity checks, package build, `Test-Package.ps1`,
  registration smoke), then publish the archive that check produced.
- `Packaging/PanelCladdingEditor/RELEASE-NOTES-1.2.5.md` holds the release text, as
  `RELEASE-NOTES-1.0.97.md` does: install steps, upgrade notes from 1.0.97, changes since 1.0.97
  and validation.
- The RHP's product version embeds the commit it was built from (`1.2.5+<commit>`), so a package
  built before a later commit names an older commit. Commit the README and release notes on a
  `codex/` branch first, run the verification on that clean commit, and tag it `v1.2.5`
  (annotated, as `v1.0.97`); the release targets that tag. The records follow in a second commit
  and the branch merges into `master` through a PR, which keeps the tagged commit in `master`.
- Only the zip channel is published. The Yak package is not built or published (no company source
  has been named).

## Affected files

- `README.md` (download instructions).
- `Packaging/PanelCladdingEditor/RELEASE-NOTES-1.2.5.md` (new).
- `Packaging/PanelCladdingEditor/README.md`: the installer section still said twelve commands; it
  writes thirteen since PCtemplatePNL (1.2.0). This README ships in the bundle, so the fix goes in
  before the verified build.
- Matching PLAN / EXET / TEST records.

## Acceptance

- `Verify-Release.ps1` passes on the committed source, with no tracked file changed.
- The published assets are byte-identical to the verified zip and sidecar: their SHA-256 values
  match the local files, and the sidecar names the zip's hash.
- The packaged RHP's product version names the tagged commit.
- The release is the latest, not a draft or prerelease, and its tag is in `master` after the
  merge. The README links resolve.
- Live Rhino acceptance is not part of this release task and is stated as not rerun.

## Rollback

Before publication, delete the branch and the generated package. After publication, keep the
versioned assets; fix problems with a new version rather than replacing assets.
