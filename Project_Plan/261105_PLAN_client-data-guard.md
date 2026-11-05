# Client data guard: hooks that keep client project data out of the repository

Execution date: 2026-11-05 (America/New_York).

## Background

The [client data cleanup](261104_PLAN_client-data-cleanup.md) rewrote history to remove client
project data. 42 of the 44 files it deleted from the tip were raw output of live runs on project
models (object and result dumps, screenshots, dialog logs) under `Project_Test/*/results*/` and
`Project_Test/*/live*/`. They got there because `tests/Live/README.md` told the live suites to write
`evidenceDir` into the tracked TEST folder, `.gitignore` ignored neither those folders nor `.json`,
`.png` or `.txt`, and nothing checked a commit or push before it reached the public remote.

After the cleanup the README still pointed `evidenceDir` into the repository. The
`repository-content-guard` smoke runs only with the regression, checks files that are already
committed, and cannot name the real clients (a list of them in a public repository is itself a
leak). The user asked that such files can never be uploaded again; screenshots no longer go into
`Project_Test/`.

## Goals

- Raw live-run output is written outside the repository by default and cannot be written into a
  git working tree.
- Commits and pushes that carry client project data are refused before they leave the machine,
  including commits made with `--no-verify` and commit messages.
- Real client names, codes and project numbers are checked without being written into the
  repository.
- One rule set serves the hooks and the regression; the regression proves the hooks work.

## Architecture ownership

Repository tooling and test code only: `.githooks/`, `.gitattributes`, `.gitignore`,
`tests/Run-Regression.ps1`, `tests/Live/`, the `repository-content-guard` smoke and the guides. No
plug-in source, version, command or GUID changes.

## Design

1. `.githooks/Test-RepositoryContent.ps1` (Windows PowerShell 5.1 compatible) with modes
   `Staged` (index vs `HEAD`), `Message` (commit message file), `Push` (every commit and blob that
   `git rev-list --objects <tips> --not --remotes=<remote>` reports, read through
   `git cat-file --batch`), `Tracked` (working-tree files) and `Commit` (one commit's tree and
   message). It reports file, line and rule but never the matched text, exits 1 when it blocks and
   2 when it fails (also blocking).
2. `.githooks/content-rules.json`: project data file types (unchanged from the smoke); raw output
   folders `Project_(Test|Archive)/**/results*|live*/`; new images under `Project_Test/` or
   `Project_Archive/` (pure renames such as archive moves stay allowed); the company
   project-folder text rules (unchanged). The smoke folder and the rules file are exempt from the
   text rules only.
3. A private client list outside the repository: `PANELCLADDING_CONTENT_DENYLIST` or
   `%APPDATA%\PanelCladding\content-denylist.txt`; plain lines match case-insensitively, `re:` lines
   are .NET regular expressions. It is checked against file content, file paths and commit
   messages. Without it the guard warns and applies the other rules. The initial list holds the two
   client projects of the cleanup, with the material category `MET-01` / `"MET"` left allowed.
4. Hooks `pre-commit`, `commit-msg`, `pre-push`: LF sh scripts (`.gitattributes`) that hand over
   to the script through `powershell.exe` (or `pwsh`). `tests/Run-Regression.ps1` sets
   `core.hooksPath = .githooks` when it is unset and warns when it names another folder.
5. `tests/Live/PcLive.cs`: `evidenceDir` defaults to
   `%LOCALAPPDATA%\PanelCladding\LiveEvidence\<yyMMdd>`, a folder inside a git working tree is
   refused, and `Start` returns the folder. README and helper usage lines follow.
6. `.gitignore` ignores `results*/` and `live*/` under `Project_Test/` and `Project_Archive/`.
7. `repository-content-guard` smoke: rule self-test from the shared JSON, hook wiring and LF check,
   `Tracked` scan of the repository, ignore check, and a scratch repository (fictional private list)
   in which the real hooks refuse and allow commits and pushes.
8. `AGENTS.md`, the plan log and `tests/README.md` state the rules; TEST folders take no new images.

## Affected files

`.githooks/` (new), `.gitattributes` (new), `.gitignore`, `tests/Run-Regression.ps1`,
`tests/Live/PcLive.cs`, `tests/Live/README.md`, `tests/Live/Hold-Workbook.ps1`,
`tests/Live/Watch-Dialogs.ps1`, `tests/Live/Show-Results.py`,
`tests/Smoke/repository-content-guard/Program.cs`, `tests/README.md`, `AGENTS.md`,
`Project_Guides/PanelCladding Plan Log.md`, and these PLAN/EXET/TEST records.

## Usage

Run `tests/Run-Regression.ps1` once per clone (or `git config core.hooksPath .githooks`); after that
every commit and push is checked. Add each new client project's name, code and number to the
private list. A blocked commit prints `path:line: rule`; fix the content rather than bypassing the
hook. `-Mode Commit -Rev <commit>` checks any commit by hand.

## Acceptance

- The guard finds nothing in the current tree and blocks the pre-cleanup tree on every rule.
- In a scratch repository the hooks refuse a workbook, a private-list name in content, file name
  and message, a company project-folder path, raw live output and a new evidence image; refuse at
  push a commit made with `--no-verify`; allow clean work and archive moves.
- `PcLive.cs` compiles against RhinoCommon 8.
- Full regression passes in Debug and Release; the commit of this change passes its own hooks.

## Rollback

Unset `core.hooksPath` and revert the commit. The private list can stay; nothing reads it then.

## Future extensions

- A server-side check (GitHub push rulesets or a required workflow) needs an organization owner
  and cannot see the private list.
- Hooks are local and can be bypassed; a second machine needs the regression run once and its own
  copy of the private list.
