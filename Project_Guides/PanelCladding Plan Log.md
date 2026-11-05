# PanelCladding PLAN / EXET / TEST records

Keep enough evidence for a later agent to see what changed, why, what was verified and what
remains. Match the records to the size of the work; not every change needs three of them.

## Choose the record level

| Work | Records |
| --- | --- |
| Substantial: a new or changed command or capability; a change to document data (keys, attributes, layers, naming); plug-in identity, packaging, installation or registration; a release; a repository-wide migration; a new test suite, hook or other repository tooling | PLAN before implementation, EXET after execution (`Record level: full`). A TEST folder only when the evidence does not fit the EXET. |
| Small: a bounded bug fix whose behaviour and rollback are clear, a documentation edit, a narrow refactor, a test-only fix, routine housekeeping | One concise EXET (`Record level: lightweight`). No PLAN and no TEST folder. |
| Using the installed plug-in in Rhino, answering a question, git housekeeping (pulls, branch cleanup) | No construction records. An archive pass writes only its manifest. |

Line count does not decide the level. If a small change grows into substantial work, write a PLAN
before going on and link the existing EXET from it.

An explicit implementation request authorizes the records its level needs; do not ask again only
to create them. The level changes the records, not the validation: Debug and Release builds, the
regression, assembly identity checks and installation attestation stay as `AGENTS.md` and the
architecture guide require. A documentation-only change needs no build; review the diff and its
links (the hooks still run). Report failed or incomplete validation as it is; never mark work
complete to fill a record.

Use the user's local date and a lowercase hyphenated `<capability-name>`; the records of one change
share it. Append retries and follow-ups to the existing record with their dates. Keep older records
as they are: do not remove the PLAN or TEST of an earlier set, or relabel it lightweight to make it
archivable.

## PLAN

For substantial work, write `Project_Plan/YYMMDD_PLAN_<capability-name>.md` before implementing.
Include background, goals, architecture ownership, design, affected files, usage, acceptance,
rollback and, when useful, future extensions. Keep it proportional to the decision.

## Tests and evidence

Put rerunnable test code in `tests/` (see `tests/README.md`): a managed smoke in
`tests/Smoke/<capability-name>/`, package checks in `tests/Package/`. Prefer focused
managed smoke projects and PowerShell verification scripts. Mark tests requiring
Rhino/native initialization or UI separately; a build does not prove live acceptance.
When a change alters behaviour that older smokes check, update those smokes in the same change;
the full suite has no accepted failures.

Record commands and results in the EXET. Only when the evidence does not fit there (long result
tables, live-run conclusions, migration maps) place it in the matching
`Project_Test/YYMMDD_TEST_<capability-name>/` folder and link it from the EXET. That folder holds no
code that is built or rerun, and since 2026-11-05 no new images: describe what a capture showed
instead. Do not create a TEST folder or README only to complete a set.

The repository is public, so records, fixtures and evidence carry no client project data.
Use fictional project codes (`ABC`, `XYZ`; `QRS`, `JKL` for another project) and generic
paths such as `C:\Projects\ABC\ABC - Wireframe.3dm`, never real project names, numbers or
company file-server paths. A live run against a real project model commits only sanitized
conclusions (step names, pass/fail, counts); its raw output (exported workbooks,
screenshots, object and layer dumps) stays out of the repository and is deleted after
the run. The live suites write it to `%LOCALAPPDATA%\PanelCladding\LiveEvidence` and refuse
a folder inside a git working tree. The hooks in `.githooks` (pre-commit, commit-msg,
pre-push; `tests/Run-Regression.ps1` sets `core.hooksPath`) run
`.githooks/Test-RepositoryContent.ps1` with the rules in `.githooks/content-rules.json`
(project data file types, `results*`/`live*` folders and new images under `Project_Test/`,
company project-folder paths) and a private client list outside the repository
(`%APPDATA%\PanelCladding\content-denylist.txt`, or `PANELCLADDING_CONTENT_DENYLIST`) that
names each real client project's name, code and number. Add every new client project to that
list; never commit it or bypass the hooks. `tests/Smoke/repository-content-guard` checks the
rules, the hook wiring, the tracked files and the hooks' behaviour.

## EXET

After execution, write `Project_Exet/YYMMDD_EXET_<capability-name>.md`.

- Full: `Record level: full`, links to the PLAN and to the TEST folder when there is one, execution
  date, changes, deviations, discovered issues, actual commands/results, acceptance alignment,
  rollback, remaining limitations and conclusion. Do not copy the PLAN into it.
- Lightweight: `Record level: lightweight`, execution date and status, then in short paragraphs or
  bullets: reason and scope, changes, validation (commands and results, or why none ran),
  remaining work and limits, related evidence (commits, PR, existing tests) and rollback when it
  is not obvious.

Do not invent test results or silently rewrite historical evidence.

## Archive

Archive completed work consistently. Keep active records in `Project_Plan/`,
`Project_Exet/` and `Project_Test/`. Once work has completed successfully and more
than three days have passed since its recorded execution date (including dated
addenda), move its records together into `Project_Archive/Project_Plan`,
`Project_Archive/Project_Exet` and `Project_Archive/Project_Test`, keeping their names: the PLAN,
EXET and TEST folder of a full or older set as it has them, a lightweight EXET on its own. A full
set without its PLAN or EXET, or an older set missing one of its three records, is unmatched.
Do not archive incomplete, failed, blocked, unmatched or still-active work; required acceptance
that the EXET records as pending, including live Rhino checks, keeps work active. Fix relative
links the move breaks, and record each archive pass in
`Project_Archive/YYMMDD_archive-manifest.md` with the sets moved and the older sets
kept, with reasons. Repository migration is not archival and preserves the original
active/archive classification.
