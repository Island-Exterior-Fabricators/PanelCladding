# PanelCladding workflow

How changes are made in this repository. Knowledge lives in a few current documents that each
change keeps up to date, not in per-change records:

| Document | Holds |
| --- | --- |
| [Architecture](PanelCladding%20Architecture.md) | The current model, boundaries and invariants of the code. |
| [Operations](PanelCladding%20Operations.md) | Build, test, packaging, installation, attestation and release procedures, with known pitfalls. |
| [Decisions](PanelCladding%20Decisions.md) | Why deliberate choices were made, so they are not undone by accident. |
| [Package README](../Packaging/PanelCladdingEditor/README.md) | User-facing command behaviour, attribute keys and code formats. |
| [README](../README.md) and `Packaging/PanelCladdingEditor/RELEASE-NOTES-<version>.md` | Features, usage and installation for users; what each release changed. |
| [tests/README.md](../tests/README.md) | How the regression suite is organized and extended. |

Git history and pull request descriptions record what changed and when.

## Making a change

1. **Agree first on substantial work**: a new or changed command or capability; a change to
   document data (keys, attributes, layers, naming); plug-in identity, packaging, installation or
   registration; a release; repository tooling. Discuss it until the scope is clear and wait for
   approval. Small, bounded fixes go ahead.
2. **Branch** `feature/<capability-name>` from `master` (lowercase, hyphenated).
3. **Implement and test.** Add or update smokes in `tests/`; when behaviour changes on purpose,
   update the smokes that check the old behaviour in the same change. The suite has no accepted
   failures.
4. **Update the documents the change affects**, in the same pull request:
   - user-visible behaviour: the package README, and the root README when usage or installation
     changes;
   - structure, data model or an invariant: the architecture guide;
   - a build, packaging, installation or release step, or a newly found pitfall: the operations
     guide;
   - a deliberate choice whose reason is not obvious from the code, or the reversal of an earlier
     one: a new entry in the decision log (mark the old entry superseded);
   - a release: its release notes.

   A change that touches none of these needs no document edit. Describe the current state; do not
   add history narratives to the guides.
5. **Validate** (below) and **open a pull request**. Its description is the change record.

## Pull request description

- **What and why**: the change and its reason, in a few sentences.
- **Validation**: the commands run and their results as summaries (pass/fail, counts), and what was
  not verified, such as live Rhino acceptance.
- **Documents updated**, or "none needed".
- **Rollback**, when it is not obvious.
- **Follow-ups**: work left open. Track it in a GitHub issue only when it must not be forgotten;
  do not keep it as an open document in the repository.

## Validation

Any change to code, tests, packaging or installer scripts needs Debug and Release builds and
`tests/Run-Regression.ps1`. A documentation-only change needs no build: review the diff and its
links (the hooks still run). Packaging and installation also need the assembly identity check and
the installation attestation gates in `AGENTS.md`; the operations guide has the steps. Report
failed or incomplete validation as it is; never describe unverified work as done.

## Public repository and client data

The repository is public. Code, fixtures, documents, commit messages, pull requests and issues
carry no client project data. Use fictional project codes (`ABC`, `XYZ`; `QRS`, `JKL` for another
project) and generic paths such as `C:\Projects\ABC\ABC - Wireframe.3dm`, never real project
names, numbers or company file-server paths. A live run against a real project model may be
reported only as sanitized conclusions (step names, pass/fail, counts). Its raw output (exported
workbooks, screenshots, object and layer dumps) stays out of the repository and is deleted after
the run; the live suites write it to `%LOCALAPPDATA%\PanelCladding\LiveEvidence` and refuse a folder
inside a git working tree.

The hooks in `.githooks` (pre-commit, commit-msg, pre-push; `tests/Run-Regression.ps1` sets
`core.hooksPath`) run `.githooks/Test-RepositoryContent.ps1` with the rules in
`.githooks/content-rules.json` (project data file types, raw live-output folders, company
project-folder paths) and a private client list outside the repository
(`%APPDATA%\PanelCladding\content-denylist.txt`, or `PANELCLADDING_CONTENT_DENYLIST`) that names each
real client project's name, code and number. Add every new client project to that list; never
commit it or bypass the hooks. `tests/Smoke/repository-content-guard` checks the rules, the hook
wiring, the tracked files and the hooks' behaviour.

## Former development records

Until 2026-11-06 each change kept PLAN, EXET and TEST records under `Project_Plan/`,
`Project_Exet/`, `Project_Test/` and `Project_Archive/`. Their still-valid content was moved into the
documents above and the folders were removed. The records remain at tag `records-2026-11-06`, for
example `git show records-2026-11-06:Project_Archive/Project_Exet/<file>.md`. Do not recreate those
folders.
