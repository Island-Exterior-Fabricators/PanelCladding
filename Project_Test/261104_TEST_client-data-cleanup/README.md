# Client data cleanup and release 1.4.1 TEST

Evidence for [the PLAN](../../Project_Plan/261104_PLAN_client-data-cleanup.md) and
[the EXET](../../Project_Exet/261104_EXET_client-data-cleanup.md). Host NYCZBF16G9NXU,
non-elevated Windows PowerShell 5.1 and Git Bash, 2026-11-04. The filter script and the list of
removed strings live outside the repository on purpose; this record names neither.

## Inventory (before the rewrite)

- 91 commits, 22 refs (master, 18 `codex/` branches, tags `v1.0.97`, `v1.2.5`, `v1.4.0`).
- No credential, token, key or password pattern in any blob of any commit.
- Client data found: two project codes (thousands of occurrences in tests, docs, records and code
  comments), their project names and numbers, company file-server paths, two exported workbooks,
  a material catalogue and a layer survey extracted from a project model, live-run JSON dumps,
  and screenshots.
- All 71 distinct screenshots and the 4 design-mockup images were reviewed one by one: 15 paths
  showed client models or project codes; 12 showed only panel numbers or dimensions without a
  project and were kept; the rest are synthetic fixtures. The design mockups are synthetic.

## History rewrite

```
git clone --bare <origin> src.git
git --git-dir=src.git fast-export --all --signed-tags=strip --tag-of-filtered-object=rewrite --reencode=yes > export.fi
python -I sanitize.py stream < export.fi > filtered.fi        # deletions, path and text rules
git --git-dir=new.git fast-import --quiet < filtered.fi
python -I sanitize.py scan new.git                             # every blob and message
git --git-dir=new.git push --force <origin> 'refs/heads/*:refs/heads/*' 'refs/tags/*:refs/tags/*'
```

Results:

- 92 commits and 22 refs in, 92 commits and 22 refs out; scan hits 0; an independent
  case-insensitive search found only the cloud-storage product name in prose.
- The rule set was applied to a checkout of `master` first and iterated until the full regression
  passed: the plug-in accepts only three-letter project codes (so `ABC`/`XYZ`, not longer words),
  the tests already used `XYZ`/`ABC` as "another project" (moved to `QRS`/`JKL`), and job codes
  also appear one letter per cell.
- First push at 17:05: a review of the `src/` diff then showed that the metal material category
  (`MET-01`, `MET-001`, `StartsWith("MET"`) had been renamed with the project code, which would
  have changed material colour matching. The rule was corrected, the rewrite rebuilt from the
  original export (differences: 7 files, material codes only), the full regression run on the
  rebuilt `master` (Debug 82/82, Release 82/82), and the corrected history force-pushed. The
  remote refs equal the rewritten refs (22/22).
- The rewritten `master` tree was byte-identical to the tested checkout before each push.
- Old history: a local bundle outside the repository, for rollback only.

## Release commit `277bf0f`

| Check | Result |
| --- | --- |
| `tests/Run-Regression.ps1` (83 smokes incl. `repository-content-guard`) | Debug 83/83, Release 83/83, exit 0, 938 s |
| `tests/Package/Verify-Release.ps1` | release smokes 6/6 per configuration, solution and RHP builds, identity checks, package build, `Test-Package.ps1`, registration test: all PASS, exit 0, 160 s, no tracked file changed |
| Packaged RHP product version | `1.4.1+277bf0fa6e94e3309bf54d20484fda2790dc92e3` |
| Plug-in GUID (Debug and Release) | `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` |
| `PanelCladdingEditor-1.4.1-win-x64.zip` SHA-256 | `b47c660fdf2dd31a1ce19a5759dc38817c8c68481aca20aa5889e5ad42c3ee97` |
| `PanelRegistryOnlyInstallationSmoke.ps1` on the 1.4.1 bundle | 15 checks OK after the stale root-`FileName` assertion was corrected (see EXET) |
| `tests/Live/PcLive.cs` | compiles against RhinoCommon 8 (scratch project); not run live |

`repository-content-guard` output: 834 tracked files with no project data file types or company
project-folder paths; its pattern self-test catches project-folder paths and accepts the
fictional fixtures.

## Publication

```
gh pr merge 19 --merge                                   # a8883d5
git tag -a v1.4.1 277bf0f && git push origin v1.4.1
gh release create v1.4.1 --verify-tag --latest -F RELEASE-NOTES-1.4.1.md <zip> <zip>.sha256
gh release download v1.4.1                               # asset SHA-256 b47c660f...ee97, matches
gh release delete v1.4.0 / v1.2.5 / v1.0.97 --yes
gh release list                                          # only v1.4.1 (Latest)
git clone --bare <origin>; sanitize.py scan              # 24 refs, 95 commits
```

The final scan of a fresh clone reports one hit: the sample strings inside the
`repository-content-guard` smoke itself (company folder-layout patterns, no client data).

## Not run

Live Rhino acceptance (plug-in code unchanged from 1.4.0 apart from comments, one example code in
a command-line message, and the version) and production installation.
