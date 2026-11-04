# Repository transfer to Island-Exterior-Fabricators EXET

## Corresponding plan and execution date

[PLAN](../Project_Plan/261104_PLAN_organization-transfer.md),
[TEST evidence](../Project_Test/261104_TEST_organization-transfer/README.md).
Executed 2026-11-04 on branch `codex/organization-transfer` from `master` (`c54c8d9`), host
NYCZBF16G9NXU.

## Changes

- GitHub: at the user's request `nuofan-island/PanelCladding` (public) and
  `nuofan-island/MCP_Rhino` (private) were transferred to `Island-Exterior-Fabricators` through
  the REST transfer endpoint as `nuofan-island`. Both completed immediately; visibility, releases,
  tags and branches were kept and the old URLs redirect.
- Local `origin` now points at `https://github.com/Island-Exterior-Fabricators/PanelCladding.git`.
- `README.md`: the MCP_Rhino link and the latest-release link name the organization.
- `Packaging/PanelCladdingEditor/README.md`: the wireframe-web and release links name the
  organization; `QUICKSTART.txt`: the releases link. Both ship in the next bundle.
- `Packaging/PanelCladdingEditor/Build-PanelCladdingEditorYak.ps1`: the Yak manifest `url` is
  `https://github.com/Island-Exterior-Fabricators/PanelCladding`.
- `WireframeWebLauncher.DownloadPage`: the PCweb download hint is
  `https://github.com/Island-Exterior-Fabricators/wireframe-web/releases/latest`.
- Version, product name, commands and GUID are unchanged.

## Deviations from plan

- The transfers and link edits were made in the same session shortly before the PLAN was written;
  the PLAN precedes all builds, tests and the commit.

## Discovered issues

- `wireframe-web` had already been moved to the organization; only its links needed updating.
- After the transfer `nuofan-island` holds push (write) but not admin on the three repositories.
  Pushing, PRs, merges and releases work; repository settings now need an organization owner, who
  can grant admin to the account or a team.
- The regression suite now has 82 smokes per configuration (77 at the 2026-10-28 transfer).

## Commands and results

See the [TEST README](../Project_Test/261104_TEST_organization-transfer/README.md). In summary:
both repositories and the PanelCladding releases resolve under the organization; `git grep
nuofan-island` finds no current reference; Debug and Release builds and the full regression
(82/82 each) passed; a scratch package build passed `Test-Package.ps1` and
`Verify-YakPackage.ps1` and carries the new links in the bundle, the Yak manifest and the RHP.

## Acceptance alignment

All PLAN acceptance items are met. Live Rhino acceptance was not rerun, as planned.

## Rollback

Revert the commit and set `origin` back to `https://github.com/nuofan-island/PanelCladding.git`.
Transferring the repositories back is a separate GitHub action by an organization owner.

## Remaining limitations

- Published release assets (`v1.0.97`, `v1.2.5`, `v1.4.0`) keep their older links; GitHub
  redirects them and the next release carries the new ones.
- PLAN / EXET / TEST and archived records keep their historical links.
- `nuofan-island` lacks admin until an organization owner grants it.

## Conclusion

PanelCladding and MCP_Rhino are owned by `Island-Exterior-Fabricators`, the local clone pushes
there, and every current link, the Yak manifest and the PCweb download hint name the organization.
