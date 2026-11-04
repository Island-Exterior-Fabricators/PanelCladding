# Client data cleanup and release 1.4.1

Execution date: 2026-11-04 (America/New_York).

## Background

The repository has been public since 2026-10-13. A review on 2026-11-04 found client project data
in the current files and in history: two client projects' codes, names and project numbers;
company file-server paths to their models; exported workbooks, a material catalogue and a layer
survey taken from their models; and screenshots and object dumps of live runs against them. No
credentials or keys were found. The published 1.0.97, 1.2.5 and 1.4.0 packages carry the same
project codes in their README examples.

The user decided: rewrite history in place (Maintain permission allows a force push; visibility is
not changed), replace the two project codes with the fictional `ABC` and `XYZ` (the plug-in only
accepts three-letter codes), delete raw live-run output instead of keeping it elsewhere, drop the
installer's cleanup of a legacy Package Manager folder whose name contains a client name, and
publish a cleaned 1.4.1 and delete the old releases.

## Goals

- No client project name, code, number, file-server path or model-derived file in any commit,
  branch, tag or commit message of the repository.
- The plug-in behaves as 1.4.0; tests keep proving the same behaviour with fictional data.
- Prevent a recurrence: a written rule and a regression check.
- Release 1.4.1 from the cleaned source and withdraw the old releases.

## Architecture ownership

Repository content, test fixtures, packaging metadata and records. Plug-in code changes are
limited to comments, one example in a command-line message, and the version.

## Design

1. History rewrite (outside the repository, script kept out of it because it names the removed
   strings): `git fast-export --all` from a bare clone, a stream filter, `git fast-import`.
   - Deleted from every commit: spreadsheets; the raw result folders of the 1.3.0 live
     acceptance, 1.4.0 release live checks and PCweb tests; the extracted material catalogue and
     its script; the evidence copy of the layer survey; 15 screenshots that show client models or
     project codes (classified one by one; screenshots of synthetic fixtures stay).
   - Replaced in every text blob, path and commit message: the two project codes in all spellings
     (upper, camel and lower case, one letter per cell) by `ABC` and `XYZ`; the existing
     "other project" examples `XYZ`/`ABC` first by `QRS`/`JKL` so the tests keep two distinct
     projects; project names and numbers; file-server roots and folder layout by generic paths.
     `MET-01`-style codes and `StartsWith("MET"` are the metal material category and stay.
   - Verified before pushing: every blob and message scanned for the removed strings, the
     rewritten `master` tree equal to a tree that passed the full regression, refs and commit count
     unchanged.
   - Force-push all branches and tags; old history kept only in a local bundle for rollback.
2. Follow-up change on `codex/client-data-cleanup`:
   - `package-manifest.json`, installer and `PanelRegistryOnlyInstallationSmoke.ps1`: only the
     `PanelCladdingEditor` legacy Package Manager root is migrated; the installer's registry
     Organization is "Panel Cladding Editor contributors".
   - `tests/Live/PcLive.cs`: the project code comes from the open model's file name (or
     `Parameters.project`), so the live suites run on any project model.
   - New smoke `tests/Smoke/repository-content-guard`: fails on project data file types and
     company project-folder paths in tracked files.
   - `.gitignore`, `AGENTS.md`, plan log: the rule and ignored file types.
   - TEST records whose raw output was removed get a dated note.
   - Version 1.4.1, `RELEASE-NOTES-1.4.1.md` (self-contained, since the old release pages go),
     README download name.
3. Release: `Verify-Release.ps1` on the committed source, GitHub release `v1.4.1` with the zip and
   its `.sha256`, then delete releases `v1.0.97`, `v1.2.5`, `v1.4.0` and their assets. Tags stay
   (rewritten).

## Affected files

History of all files listed above; in the follow-up commit: package manifest, installer, package
README, release notes, README, csproj, `tests/Live/PcLive.cs`, the registry smoke, the new guard
smoke, `.gitignore`, `AGENTS.md`, the plan log, seven TEST records, and these PLAN/EXET/TEST
records.

## Usage

Clones made before 2026-11-04 must be deleted and cloned again; their history still holds the
removed data and must not be pushed. Live suites take the project from the model file name.

## Acceptance

- Scan of every blob and message in the pushed repository: no hit.
- Full regression (83 smokes) passes in Debug and Release on the release commit.
- `Verify-Release.ps1` passes; the RHP reports `1.4.1+<commit>` and the plug-in GUID.
- GitHub lists only release `v1.4.1`; `git ls-remote` matches the rewritten refs.

## Rollback

The pre-rewrite history is in a local bundle outside the repository; force-pushing it back
restores the old state (and the data). Old release assets cannot be restored once deleted; they
can be rebuilt from their tags.

## Future extensions

GitHub keeps pull-request refs and cached views of old commits. Purging them needs an
organization owner to contact GitHub Support.
