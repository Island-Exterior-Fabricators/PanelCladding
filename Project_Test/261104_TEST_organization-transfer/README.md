# Repository transfer to Island-Exterior-Fabricators TEST

Evidence for [the PLAN](../../Project_Plan/261104_PLAN_organization-transfer.md) and
[the EXET](../../Project_Exet/261104_EXET_organization-transfer.md). Host NYCZBF16G9NXU,
non-elevated Windows PowerShell 5.1, 2026-11-04, branch `codex/organization-transfer` from
`master` `c54c8d9` with the link edits uncommitted.

## Commands

```powershell
gh api repos/Island-Exterior-Fabricators/wireframe-web --jq '{full_name, private, owner: .owner.login}'
gh api -X POST repos/nuofan-island/PanelCladding/transfer -f new_owner=Island-Exterior-Fabricators
gh api -X POST repos/nuofan-island/MCP_Rhino/transfer -f new_owner=Island-Exterior-Fabricators
gh api repos/Island-Exterior-Fabricators/PanelCladding --jq '{full_name, visibility, default_branch, permissions}'
gh api repos/Island-Exterior-Fabricators/MCP_Rhino --jq '{full_name, visibility}'
gh api repos/nuofan-island/<PanelCladding|MCP_Rhino|wireframe-web> --jq '.full_name'   # old names
gh release list --repo Island-Exterior-Fabricators/PanelCladding
git remote set-url origin https://github.com/Island-Exterior-Fabricators/PanelCladding.git
git fetch origin
git grep -n -i nuofan-island -- . ':!Project_Plan' ':!Project_Exet' ':!Project_Test' ':!Project_Archive'
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release
.\tests\Run-Regression.ps1
$out = '<scratchpad>\org-transfer-build'
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorYak.ps1 -OutputRoot $out   # build only, no -Source
.\tests\Package\Test-Package.ps1 -Archive "$out\PanelCladdingEditor-1.4.0-win-x64.zip"
.\tests\Package\Verify-YakPackage.ps1 -PackagePath "$out\panelcladdingeditor-1.4.0-rh8_33-win.yak"
# extracted ZIP: Select-String README.md, QUICKSTART.txt -Pattern 'github\.com';
# UTF-16 search of the bundled PanelCladdingEditor.rhp for the new and old URLs
```

## Results

GitHub before the transfer:

- The account `nuofan-island` is an active member of `Island-Exterior-Fabricators` (display name
  "Island Exterior Fabricators", enterprise plan); members may create public and private
  repositories. Neither `PanelCladding` nor `MCP_Rhino` existed in the organization.
- `wireframe-web` was already `Island-Exterior-Fabricators/wireframe-web` (private);
  `repos/nuofan-island/wireframe-web` resolves to it.

GitHub after the transfer (completed immediately, no acceptance step):

- `Island-Exterior-Fabricators/PanelCladding`: public, default branch `master`.
- `Island-Exterior-Fabricators/MCP_Rhino`: private.
- `repos/nuofan-island/PanelCladding`, `.../MCP_Rhino` and `.../wireframe-web` resolve to the
  organization repositories.
- Releases `v1.4.0` (latest, 2026-11-01T18:18:00Z), `v1.2.5` and `v1.0.97` are listed under the
  organization; `git ls-remote --tags origin` lists `v1.2.5` and `v1.4.0`.
- `nuofan-island` now has pull, triage and push on all three repositories, and no admin or
  maintain permission.

Local repository:

- `origin` fetch and push URLs are `https://github.com/Island-Exterior-Fabricators/PanelCladding.git`;
  the fetch succeeded.
- `git grep nuofan-island` outside the PLAN / EXET / TEST and archive folders: no match (exit 1).

Builds and regression:

- `dotnet build` Debug and Release: 0 errors.
- `Run-Regression.ps1` (exit 0, 511 s including the two builds): Debug 82/82 and Release 82/82
  passed.

Package (built into the scratchpad, not installed or published):

- Zip and Yak build succeeded in 12 s. Compiled assembly identity
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` in Release. RHP product version
  `1.4.0+c54c8d9398c2ca9b29a9923e46a96bcef30e0c20` (uncommitted working tree on `c54c8d9`);
  this local build is evidence only.
- `Test-Package.ps1`: ZIP checksum, all 32 payload hashes, Windows x64 dependencies, compiled
  identities, and the extracted-ZIP install/repair/validate/uninstall in Windows PowerShell 5.1
  passed.
- `Verify-YakPackage.ps1`: all Yak package checks passed, including the packaged RHP GUID match,
  temporary folder-source publish and search, immutable republish and refusal of the public server.
- Links in the built output:
  - Yak `manifest.yml`: `url: https://github.com/Island-Exterior-Fabricators/PanelCladding`.
  - `QUICKSTART.txt:44`: `https://github.com/Island-Exterior-Fabricators/PanelCladding/releases`.
  - `README.md:49`: `https://github.com/Island-Exterior-Fabricators/wireframe-web/releases`.
  - `README.md:527`: `https://github.com/Island-Exterior-Fabricators/PanelCladding/releases/latest`.
  - The bundled `PanelCladdingEditor.rhp` contains
    `https://github.com/Island-Exterior-Fabricators/wireframe-web/releases/latest` and no
    `nuofan-island` string.

Not run: live Rhino acceptance and production installation (PCweb's launch logic is unchanged;
only the download hint URL changed).
