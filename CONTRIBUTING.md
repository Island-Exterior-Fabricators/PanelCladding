# Contributing to PanelCladding

This guide describes how changes are made and checked in this repository.
It applies equally to people and to coding agents.

## The check command

One command checks the whole repository:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check.ps1
```

It runs every check below, prints a `FAILED:` line for each failing step, and exits non-zero when anything fails.

- It makes sure the .NET SDK pinned in `global.json` is present, and otherwise downloads it and verifies it against the SHA-512 pinned in `tools/dotnet-sdk.json`.
- It runs the client data guard on every tracked file (`.githooks/Test-RepositoryContent.ps1 -Mode Tracked`).
- It runs the static checks in `tests/Checks/`.
- It runs `tools/RepoCheck`, which fails on any comment, any prose line that holds two sentences or wraps a sentence, and any test script that sleeps or waits with a time limit.
- It builds `PanelCladding.sln` in Debug and Release with warnings as errors and NuGet restore in locked mode, so packages come only from the committed `packages.lock.json` files.
- It builds the production RHP in both configurations and checks its assembly-level plug-in GUID (`tests/Package/Verify-PluginAssemblyIdentity.ps1`).
- It builds and runs every managed smoke in Debug and Release (`tests/Run-Regression.ps1`).

The same command runs in CI ([`.github/workflows/check.yml`](.github/workflows/check.yml)), in Super Agent verification ([`.superagent2/check.json`](.superagent2/check.json)) and in the pre-push hook.
Run it before you open a pull request.

It needs Windows, Rhino 8 at `C:\Program Files\Rhino 8` (the projects reference its RhinoCommon) and network access the first time the pinned SDK is missing.
The SDK download is kept under `%LOCALAPPDATA%\PanelCladding\toolchain`, or under `PANELCLADDING_TOOLCHAIN_ROOT` when that is set.
When `LOCALAPPDATA` or `APPDATA` is not set, as in a fresh verification checkout, the check uses folders under the git-ignored `artifacts/check-home` instead.

Some tests need a running Rhino, an installed plug-in or a user session, so the check command leaves them out.
Run them by hand when a change touches what they cover: the live suites in `tests/Live/`, the probes in `tests/Probes/`, and the package, installer, registration and Yak checks in `tests/Package/`.
The [operations guide](docs/operations.md) says when each one is needed.

## Code, documents and tests

- No file holds comments, including scripts, configuration and Markdown.
  Names, tests and the documents carry the intent; a reason worth keeping goes into the [design document](docs/design.md) or the [decision log](docs/decisions.md).
- Documents are written one sentence per line, and a sentence is never wrapped.
- Tests wait for an event, never for a duration, have no time limit of their own and never assert how long something took.
  Code that depends on time takes a `TimeProvider`, and tests drive it with `tests/Shared/ManualTimeProvider.cs`; the analyzer list `tests/BannedSymbols.txt` keeps the system clock, sleeps and timed waits out of C# tests.
- Generated files and other exclusions from these rules are listed in one place, `tools/RepoCheck/Exclusions.cs`.

## Setting up a clone

1. Run `powershell -NoProfile -ExecutionPolicy Bypass -File tools/install-hooks.ps1` once in each clone and worktree.
   It points `core.hooksPath` at `.githooks`, so the pre-commit and commit-msg hooks run the client data guard and the pre-push hook runs the guard and then the check command.
   It exits 1 with a warning when the clone uses another hooks folder; run it again whenever you want to confirm that your clone runs its own hooks.
2. If you work on client models, keep the private client list at `%APPDATA%\PanelCladding\content-denylist.txt` (see [Public repository and client data](#public-repository-and-client-data)).
3. After adding or changing a NuGet package reference, build once without `RestoreLockedMode` (a normal `dotnet build` or Visual Studio build) so the lock files are updated, and commit the changed `packages.lock.json` files.

## CI

The workflow runs on pull requests, on pushes to `master` and on manual start, on a self-hosted Windows runner with the labels `self-hosted`, `windows` and `rhino`.
It runs only for pull requests whose branch is in this repository, so code from a fork never runs on the self-hosted runner.
A maintainer who wants CI on a fork's change pushes that branch to this repository first.
Registering the runner and protecting `master` are done by the repository owner.

## Documents

| Document | Holds |
| --- | --- |
| [Design](docs/design.md) | The current model, boundaries and invariants of the code, and the closed list of enforced rules. |
| [Operations](docs/operations.md) | Build, test, packaging, installation, attestation and release procedures, with known pitfalls. |
| [Decisions](docs/decisions.md) | Why deliberate choices were made, newest last, so they are not undone by accident. |
| [Package README](Packaging/PanelCladdingEditor/README.md) | User-facing command behaviour, attribute keys and code formats. |
| [README](README.md) and `Packaging/PanelCladdingEditor/RELEASE-NOTES-<version>.md` | Features, usage and installation for users, and what each release changed. |
| [tests/README.md](tests/README.md) | How the regression suite is organized and extended. |

Git history and pull request descriptions record what changed and when.
Documents are written in English, one sentence per line.

## Making a change

1. Agree first on substantial work: a new or changed command or capability, a change to document data (keys, attributes, layers, naming), plug-in identity, packaging, installation or registration, a release, or repository tooling.
   Small, bounded fixes go ahead.
2. Branch from `master`; `feature/<capability-name>` (lowercase, hyphenated) is the usual name.
3. Implement and test.
   Add or update smokes in `tests/`, and when behaviour changes on purpose, update the smokes that check the old behaviour in the same change.
   The suite has no accepted failures, and a failure is never fixed by weakening a test or skipping a check.
4. Update the documents the change affects in the same pull request:
   - user-visible behaviour: the package README, and the root README when usage or installation changes;
   - structure, data model or an invariant: the design document;
   - a build, packaging, installation or release step, or a newly found pitfall: the operations guide;
   - a deliberate choice whose reason is not obvious from the code, or the reversal of an earlier one: a new entry at the end of the decision log, marking the old entry superseded;
   - a release: its release notes.

   Describe the current state, and do not add history narratives to the guides.
5. Run the check command, then open a pull request.
   Its description is the change record.

## Pull request description

- **What and why**: the change and its reason, in a few sentences.
- **Validation**: the commands run and their results as summaries (pass or fail, counts), and what was not verified, such as live Rhino acceptance.
- **Documents updated**, or "none needed".
- **Rollback**, when it is not obvious.
- **Follow-ups**: work left open, tracked in a GitHub issue when it must not be forgotten.

Squash merge is preferred, so that each change is one commit on `master`.
A release pull request is the exception: it is merged with a merge commit so that the tagged commit is in `master` ([operations guide](docs/operations.md#release-procedure)).

## Public repository and client data

The repository is public.
Code, fixtures, documents, commit messages, pull requests and issues carry no client project data.
Use fictional project codes (`ABC`, `XYZ`, and `QRS` or `JKL` for another project) and generic paths such as `C:\Projects\ABC\ABC - Wireframe.3dm`, never real project names, numbers or company file-server paths.
A live run against a real project model may be reported only as sanitized conclusions (step names, pass or fail, counts).
Its raw output (exported workbooks, screenshots, object and layer dumps) stays out of the repository and is deleted after the run.
The live suites write it to `%LOCALAPPDATA%\PanelCladding\LiveEvidence` and refuse a folder inside a git working tree.

The hooks in `.githooks` run `.githooks/Test-RepositoryContent.ps1` with the rules in `.githooks/content-rules.json` (project data file types, raw live-output folders, company project-folder paths) and a private client list outside the repository.
That list is `%APPDATA%\PanelCladding\content-denylist.txt`, or the file named by `PANELCLADDING_CONTENT_DENYLIST`, and it names each real client project's name, code and number.
Add every new client project to that list, and never commit it or bypass the hooks.
The `repository-content-guard` smoke checks the rules, the hook wiring, the tracked files and the hooks' behaviour.

## Former development records

Until 2026-11-06 each change kept PLAN, EXET and TEST records under `Project_Plan/`, `Project_Exet/`, `Project_Test/` and `Project_Archive/`.
Their still-valid content was moved into the documents above and the folders were removed.
The records remain at tag `records-2026-11-06`, for example `git show records-2026-11-06:Project_Archive/Project_Exet/<file>.md`.
Those folders are not recreated.
