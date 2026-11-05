# Client data guard TEST

Evidence for [the PLAN](../../Project_Plan/261105_PLAN_client-data-guard.md) and
[the EXET](../../Project_Exet/261105_EXET_client-data-guard.md). Host NOVAN-HOME, Windows PowerShell
5.1.26100, git 2.53.0.windows.3, .NET SDK 8.0.420, 2026-11-05. The private client list lives outside
the repository; this record names none of its entries, only how many there are.

## Root cause (pre-cleanup history, local reflog)

`git diff --diff-filter=D` between the pre-cleanup `master` and its rewritten equal: 44 files deleted,
42 of them under `Project_Test/` or `Project_Archive/Project_Test/` (23 `.json`, 15 `.png`, 3 `.txt`,
2 `.xlsx`, 1 `.py`), mostly in `results*/` and `live*/` folders. Six commits between 2026-10-28 and
2026-11-01 added them together with their TEST records. `tests/Live/README.md` set `evidenceDir` to
`<repo>\Project_Test\<date>_TEST_live-acceptance-tests\results`; there were no hooks and no CI.

## Guard on known trees

| Run | Result |
| --- | --- |
| `-Mode Tracked`, this checkout | 838 files clean, 7 private list entries, exit 0, 1.1 s |
| `-Mode Commit -Rev origin/master` (cleaned) | 838 files clean, exit 0, 1.2 s |
| `-Mode Commit` on the pre-cleanup `master` | exit 1, 328 findings; every whole-tree rule hit: 7/7 private entries, file types, raw output folders, all four path-shape rules (the new-image rule applies to commits and pushes only) |
| `-Mode Push`, pre-cleanup `master` as a new branch (stdin only, nothing sent) | exit 1, 540 findings in 88 commits, 7 of them in commit messages, 2.2 s |
| `-Mode Push`, this branch with no new commits; a branch deletion | exit 0 each |
| `-Mode Tracked` from the Terminal panel (host side, after the list was moved there) | 838 files clean, 7 private list entries |

The first host-side run reported no private list: a file the Claude session writes under `%APPDATA%`
is not visible to the host. The list was written into the project folder and moved to
`%APPDATA%\PanelCladding\` from the Terminal panel.

## Hooks in a scratch repository (by hand, then in the smoke)

Real `git commit` / `git push` through sh and Windows PowerShell, fictional private list:

| Case | Expected | Result |
| --- | --- | --- |
| Hook files, a fictional note | allowed | allowed |
| `.xlsx` / `.xlsm` workbook | refused: project data file type | refused |
| Private-list name in content (any case), in a file name, in a commit message | refused | refused; output names file, line and entry number only |
| Case-sensitive `re:` entry, same letters in lower case | refused / allowed | as expected |
| Company project-folder path (drive root, project number and name; fictional) | refused: company project-folder root | refused |
| `Project_Test/.../results/bake.json` (added with `-f`) | refused: raw live-run output folder | refused |
| New `Project_Test/.../shot.png` | refused: new image in test evidence | refused |
| Existing image moved into `Project_Archive/` (commit and push) | allowed | allowed |
| Commit made with `--no-verify` carrying a private-list name in a file / in its message | push refused | refused |
| Clean push, push with nothing new | allowed | allowed |
| No private list | warning, other rules apply | warning; workbook still refused |

## Other checks

| Check | Result |
| --- | --- |
| `.githooks/pre-commit`, `commit-msg`, `pre-push` | `i/lf w/lf attr/text eol=lf` |
| `tests/Live/PcLive.cs` | compiles against RhinoCommon 8 (scratch project, net7.0-windows); not run live |
| `tests/Run-Regression.ps1 -Configuration Debug -Name repository-content-guard` | PASS, 23.6 s including build |
| `tests/Run-Regression.ps1` (all smokes, change staged) | Debug 83/83, Release 83/83, exit 0, 319 s; the guard smoke scanned 845 tracked files with 7 private list entries |
| `dotnet build src/PanelCladdingEditor` Debug and Release | succeeded, 0 warnings, 0 errors (no source change) |
| Commit of this change | first attempt refused by the new pre-commit hook (this README quoted a fictional company project-folder path, line 41: `company project-folder root`); after rewording, passed pre-commit and commit-msg |

## Not run

Live Rhino suites (the change only moves where they write and refuses folders inside a working
tree) and any GitHub-side rule.
