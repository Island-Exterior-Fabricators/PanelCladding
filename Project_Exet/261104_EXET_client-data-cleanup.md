# Client data cleanup and release 1.4.1 EXET

## Corresponding plan and execution date

[PLAN](../Project_Plan/261104_PLAN_client-data-cleanup.md),
[TEST evidence](../Project_Test/261104_TEST_client-data-cleanup/README.md).
Executed 2026-11-04 on host NYCZBF16G9NXU. History rewritten from `master` after
[PR 18](https://github.com/Island-Exterior-Fabricators/PanelCladding/pull/18) was merged; follow-up
on branch `codex/client-data-cleanup`, release commit `277bf0f`.

## Changes

- History of all 22 refs rewritten and force-pushed: client project data removed from every
  commit, branch, tag and commit message (see the PLAN for the rules). Commit SHAs changed;
  tags `v1.0.97`, `v1.2.5`, `v1.4.0` point at the rewritten commits.
- Package manifest, installer and registry smoke: only the `PanelCladdingEditor` legacy Package
  Manager root is migrated; the registry Organization is "Panel Cladding Editor contributors".
- `tests/Live/PcLive.cs`: the project root comes from the model file name or `Parameters.project`.
- New smoke `tests/Smoke/repository-content-guard`; `.gitignore`, `AGENTS.md` and the plan log
  carry the rule that client data stays out of the public repository.
- Seven TEST records whose raw output was removed carry a dated note.
- Version 1.4.1, `RELEASE-NOTES-1.4.1.md`, README download name.

## Deviations from plan

- The user first chose the codes `DEMO` and `SAMPLE`; the plug-in accepts only three-letter
  project codes (six smokes failed), and the user then chose `ABC` and `XYZ`.
- The first force-push carried a wrong replacement of the metal material category
  (`MET-01`-style codes and the metal colour check). It was found in a review of the `src/` diff
  about half an hour later, corrected, retested and force-pushed again. Anyone who fetched in
  between must re-clone.
- `PanelRegistryOnlyInstallationSmoke.ps1` is not run by any suite. Running it on the 1.4.1 bundle
  showed a stale assertion from before this change: it expected a root `FileName`, which the
  installer has rejected as shorthand registration since the registration work. The assertion now
  checks that the root has none; the legacy-root assertions changed here passed as written.

## Discovered issues

- GitHub keeps refs of merged pull requests (`refs/pull/*`) and cached views of old commits; they
  still reach the old objects. Removing them needs an organization owner to ask GitHub Support.
- The account has Maintain, not Admin; visibility was not changed, as decided.

## Commands and results

See the [TEST README](../Project_Test/261104_TEST_client-data-cleanup/README.md): scans 0 hits,
full regression 83/83 in Debug and Release, `Verify-Release.ps1` passed, RHP
`1.4.1+277bf0fa6e94e3309bf54d20484fda2790dc92e3`, zip SHA-256
`b47c660fdf2dd31a1ce19a5759dc38817c8c68481aca20aa5889e5ad42c3ee97`.

## Acceptance alignment

All PLAN acceptance items are met. After
[PR 19](https://github.com/Island-Exterior-Fabricators/PanelCladding/pull/19) was merged
(`a8883d5`), annotated tag `v1.4.1` was created on the verified commit `277bf0f` and the GitHub
release `PanelCladdingEditor 1.4.1` was published as latest with the verified zip and its
`.sha256`; the downloaded asset matches the recorded SHA-256. Releases `v1.4.0`, `v1.2.5` and
`v1.0.97` and their assets were deleted; their (rewritten) tags remain. Live Rhino acceptance was
not run, as planned.

## Rollback

Force-push the local pre-rewrite bundle (restores the data as well). Revert the follow-up commit
to return to 1.4.0 content.

## Remaining limitations

- Clones and forks made before the rewrite still hold the old history; delete and re-clone.
- PR refs and caches on GitHub as above.
- Live Rhino acceptance of 1.4.1 was not run; production installation is the user's step.

## Conclusion

The repository's branches, tags and messages no longer contain client project data, tests prove
the same behaviour with fictional projects, and a regression check guards against new leaks.
