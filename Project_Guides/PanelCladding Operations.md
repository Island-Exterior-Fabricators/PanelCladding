# PanelCladding operations

How to build, test, package, install, attest and release PanelCladdingEditor, for maintainers and
coding agents. End-user install steps are in the root [README](../README.md#install). The scripts
named here are the source of truth: when this page and a script disagree, the script wins.

## Quick reference

| Item | Value |
| --- | --- |
| Product / assembly | `PanelCladdingEditor` (`src/PanelCladdingEditor`, .NET 8 WPF, Windows x64, Rhino 8) |
| Version | `<Version>` in the csproj and `version` in `Packaging/PanelCladdingEditor/package-manifest.json`; must be equal, `major.minor.patch` |
| Plug-in GUID | `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35` |
| Commands (13) | `PCbake PCclear PCcreate PCcrvTemplate PCeditor PCmatchCrv PCmatchSrf PCpid PCreport PCsyncCrv PCsyncSrf PCtemplatePNL PCweb` |
| Zip install | `%LOCALAPPDATA%\PanelCladdingEditor\plugin\<version>\`; ownership manifest `install-manifest.json` and `rollback\<installId>\` beside `plugin\` |
| Registration key | `HKCU\Software\McNeel\Rhinoceros\8.0\Plug-Ins\7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` |
| Yak install (Rhino-owned) | `%APPDATA%\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\<version>` |
| Live-run raw output | `%LOCALAPPDATA%\PanelCladding\LiveEvidence\<yyMMdd>` (never in the repository) |

## Build

Requires the .NET 8 SDK and Rhino 8 at `C:\Program Files\Rhino 8` (the RhinoCommon `HintPath`;
RhinoCommon is never shipped). Warnings are errors.

- A normal build produces `PanelCladdingEditor.rhp`, the file Rhino loads. With
  `PanelCladdingTestHost=true` the same project builds `PanelCladdingEditor.dll`, which every smoke
  references.
- Both variants build into `src/PanelCladdingEditor/bin/<Configuration>/net8.0-windows/` and the
  same `obj/`. Build serially (`-m:1`); parallel builds race (MSB3030 copy errors). After a smoke or
  solution build that folder can hold only the test-host DLL, so rebuild the production project last
  before identity checks or packaging. The package script and `Verify-Release.ps1` do this.

```powershell
dotnet build .\PanelCladding.sln -c Release -m:1                                          # likewise Debug
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release -t:Rebuild   # restore the RHP
```

## Managed regression

```powershell
.\tests\Run-Regression.ps1                                  # every smoke, Debug and Release
.\tests\Run-Regression.ps1 -Configuration Debug -Name panel-cladding-pid*
.\tests\Run-Regression.ps1 -List                            # also -NoBuild, -TimeoutSeconds (default 900)
```

- It first sets `core.hooksPath = .githooks` when unset (only warns if another folder is set): run
  it once per clone. It then builds each smoke serially into `artifacts/tests/bin/<Configuration>/`,
  runs it from the repository root with stdin closed, logs to `artifacts/tests/logs/<Configuration>/`,
  writes `artifacts/tests/regression-<Configuration>.json`, and exits 1 on any failure (no accepted
  failures). Rhino-native parts print `[SKIP]`; a managed pass is not live acceptance.
- No suite runs `tests/Package/PanelRegistryOnlyInstallationSmoke.ps1` or
  `tests/Package/Verify-YakPackage.ps1`; run them by hand after changing the installer or the Yak
  script. Static checks: `tests/Checks/Test-ViewportHighlightDrawing.ps1` and
  `tests/Checks/Verify-Migration.py` (GUID text, no external or MCP_Rhino project references).

## Assembly identity

Rhino takes the plug-in id from the assembly-level `GuidAttribute` (`src/PanelCladdingEditor/AssemblyInfo.cs`);
without it the id is `Guid.Empty` and collides with any other RHP that omits it (`ID already in
use`). The `[Guid]` on the plug-in class alone is not enough. The assembly GUID, the
`PanelCladdingEditorPlugin` class GUID and the manifest `pluginId` must agree;
`tests/Package/Verify-PluginAssemblyIdentity.ps1` reads the compiled PE metadata and also requires
the compiled version to equal the manifest version. The HKCU key name and the Yak `guid:` keyword
derive from `pluginId`; the zip bundle's `manifest.yml` keyword is hard-coded in
`Build-PanelCladdingEditorPackage.ps1`.

```powershell
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -Configuration Release          # builds the RHP first
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -PluginPath <bundle>\Plugin\PanelCladdingEditor.rhp
```

The package build checks the Release output; `Test-Package.ps1` and `Verify-YakPackage.ps1` check
the packaged RHP. The installer checks only the assembly name, so run the `-PluginPath` form on any
bundle you did not just verify before installing it.

## Zip package

```powershell
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1      # -Version, if given, must equal the manifest
.\tests\Package\Test-Package.ps1 -Archive .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-<version>-win-x64.zip
```

The build publishes, rebuilds the RHP, checks identity and filters dependencies into
`Packaging/PanelCladdingEditor/artifacts/` (git-ignored): the bundle `PanelCladdingEditor-<version>/`
(`Install.cmd`, `Uninstall.cmd`, `QUICKSTART.txt`, `README.md`, `manifest.yml`,
`package-manifest.json`, `bundle-manifest.json` with every file's SHA-256, `Installer\`, `Plugin\`),
plus `PanelCladdingEditor-<version>-win-x64.zip` and its `.zip.sha256`. `Plugin\` holds the RHP, its
`.deps.json`, private DLLs, and the Skia/HarfBuzz natives both at top level (Rhino's load context
needs them) and under `runtimes/win-x64/native/`; never a `PanelCladdingEditor.dll` twin,
RhinoCommon, Rhino.UI, Eto, `.pdb`, other runtimes or MCP dependencies.

`Test-Package.ps1` checks the checksum, exact file list, hashes and packaged identity, then runs
`Test-UserInstallation.ps1` under Windows PowerShell 5.1 against throwaway `%LOCALAPPDATA%`/HKCU
roots (install, validate, repair, HKLM rejection, injected-failure rollback, `-WhatIf`, uninstall).
That script refuses an elevated token: run package checks non-elevated.

## Zip-channel installer

`Installer\Install-PanelCladdingEditor.ps1 -Mode Install|Repair|Validate` (default `Install`).
Uninstall is a separate script, `Installer\Uninstall-PanelCladdingEditor.ps1` (supports `-WhatIf`).
`Install.cmd` runs `Install-Package.ps1`, which refuses to run while Rhino is open, runs Install and
then Validate, and ends with `pause`. `Uninstall.cmd` runs the uninstaller.

**Install / Repair** (one code path; Repair reinstalls the extracted bundle's version):

1. Checks bundle hashes, name/version and `registry-only` mode; the RHP must have no DLL twin and a
   `.deps.json` naming the `.rhp`.
2. If Rhino is running (default roots only), copies the bundle to
   `%LOCALAPPDATA%\PanelCladdingEditor\staged\<version>-<utc>\`, prints the command to run after
   Rhino closes, and stops without installing.
3. Refuses an unowned plug-in tree (no ownership manifest), any other discoverable
   `PanelCladdingEditor.rhp` (`packages\8.0`, Rhino 8 `Plug-ins` folders in `%APPDATA%` and
   `%LOCALAPPDATA%`), a GUID key naming another product, a non-HKCU registry root, and default file
   roots mixed with a non-default registry root.
4. Exports the old key to `rollback\<id>\prior-registration.reg`, moves the current version folder
   to `rollback\<id>\prior-active` and other versions to `prior-plugin-<version>`, activates
   `plugin\<version>`, then deletes and rewrites the whole key (table below).
5. Moves the legacy Package Manager folder `packages\8.0\PanelCladdingEditor` (where a Yak install
   lives) to `rollback\<id>\legacy-package-PanelCladdingEditor`, validates, and writes
   `install-manifest.json`. On failure it removes the new key and tree, restores the backups and
   imports the old `.reg`.

**Validate** must run from the extracted bundle of the installed version (another version fails
with "ownership manifest is missing or incompatible"). It checks manifest and file hashes, the
direct RHP, the exact registration, and single discovery (one RHP under `plugin\`, none under
`packages\8.0`).

**Uninstall** refuses while Rhino runs. It removes the key only while still owned (`Name`
`PanelCladdingEditor`, `PlugIn\FileName` = the owned RHP), deletes only files with unchanged
hashes, keeps the ownership manifest if anything was left, and always keeps `rollback\`.

### Registry: written vs owned

| Key | Written by the installer | After Rhino starts |
| --- | --- | --- |
| Root: `Name`/`EnglishName` = `PanelCladdingEditor`, `Organization`, `Description`, empty contact strings, `RegPath` = `\\HKEY_CURRENT_USER\...\<guid>`, DWORD `LoadMode=1`, `Type=16`, `IsDotNETPlugIn=1`, `DirectoryInstall=0`, `AddToHelpMenu=0`; no root `FileName` | yes | rewritten by Rhino on every start |
| `PlugIn\FileName` = full path of the active RHP | yes | never written by Rhino; installer-owned, does not self-heal |
| `CommandList`: exactly the 13 commands, value `2;<name>` | yes | rewritten by Rhino on every start |

Validate rejects a shorthand root `FileName`, a missing `PlugIn` or `CommandList`, a missing or
extra command, or a wrong value. Command names compare case-insensitively.

Rhino refreshes the root and `CommandList` even when `PlugIn\FileName` is stale and nothing loads,
so a fresh root timestamp proves nothing. Only Validate, or an independent read of
`PlugIn\FileName`, catches a dangling pointer.

Adding or renaming a command means updating `Get-PanelExpectedCommands` in the installer,
`Test-PanelCladdingPluginRegistration.ps1`, `Test-UserInstallation.ps1`, the
`panel-cladding-pc-commands` smoke, `tests/Live/PcLive.cs` and the package README.

## Production install and host attestation

An install counts as done only after an independent host read. Without one, build and stage only,
and leave the installed RHP where it is.

1. **Stage.** Extract the verified zip outside the repository; every file hash must equal the
   verified bundle.
2. **Install.** Close Rhino; run `Install.cmd` from Explorer, or
   `powershell -NoProfile -ExecutionPolicy Bypass -File <staged>\Installer\Install-Package.ps1` in a
   host terminal (`Install.cmd` ends with `pause`). Never from the agent's sandboxed shell.
3. **Check the registry** from a separate host process after the installer exits: `-Mode Validate`
   passes; `PlugIn\FileName` points to `plugin\<version>`, exists and hashes to the verified RHP;
   `CommandList` holds exactly the 13 commands; all three key timestamps (root, `PlugIn`,
   `CommandList`) are at or after the install.
4. **Check after a Rhino start** (from a host shell): the exact RHP is loaded; root and
   `CommandList` timestamps advanced; `PlugIn` unchanged.

```powershell
# Host shell only, after the installer process has exited.
$sub = 'Software\McNeel\Rhinoceros\8.0\Plug-Ins\7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35'
Add-Type @'
using System; using System.Runtime.InteropServices; using Microsoft.Win32; using Microsoft.Win32.SafeHandles;
public static class KeyTime {
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    static extern int RegQueryInfoKey(SafeRegistryHandle k, IntPtr a, IntPtr b, IntPtr c, IntPtr d,
        IntPtr e, IntPtr f, IntPtr g, IntPtr h, IntPtr i, IntPtr j, out long t);
    public static DateTime Read(RegistryKey k) {
        long t; int r = RegQueryInfoKey(k.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out t);
        if (r != 0) throw new System.ComponentModel.Win32Exception(r);
        return DateTime.FromFileTimeUtc(t);
    }
}
'@
foreach ($name in 'root', 'PlugIn', 'CommandList') {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($(if ($name -eq 'root') { $sub } else { "$sub\$name" }))
    try { '{0,-12} {1:o}' -f $name, [KeyTime]::Read($key) } finally { $key.Close() }
}
$rhp = (Get-ItemProperty "HKCU:\$sub\PlugIn").FileName
$rhp; Test-Path -LiteralPath $rhp; (Get-FileHash -LiteralPath $rhp).Hash
reg query "HKCU\$sub\CommandList"        # exactly 13 values, each 2;<name>
& <staged>\Installer\Install-PanelCladdingEditor.ps1 -Mode Validate
# After Rhino starts (then repeat the timestamp loop):
Get-Process Rhino | ForEach-Object { $_.Modules } | Where-Object ModuleName -like 'PanelCladdingEditor*' | Select-Object FileName
```

Roll back by installing the earlier verified bundle with its own `Install.cmd`; its folder is also
kept under `rollback\`.

## Sandboxed agent shells

An agent's tool shell sees a private, virtualized HKCU. Registry writes made there, and read-backs
there, are not host evidence; this held even with the tool sandbox disabled. File moves under
`%LOCALAPPDATA%` were real. As a result, an installer run in the sandbox moved the old RHP to
`rollback\` while the real registry still pointed at it, and Rhino silently stopped loading the
plug-in. Files the session wrote under `%APPDATA%` were not visible to the host.

- Never run `Install.cmd`, `Install-Package.ps1`, `-Mode Install|Repair` or the uninstaller against
  the default roots from the sandbox. Build and stage there; install and attest in a host shell:
  the desktop app's Terminal panel, a user-started PowerShell, or Explorer.
- Package checks with throwaway roots (`Test-Package.ps1`, `Test-UserInstallation.ps1`,
  `Test-PanelCladdingPluginRegistration.ps1`, `PanelRegistryOnlyInstallationSmoke.ps1`) are safe in
  the sandbox.
- Start Rhino from a host shell; a Rhino started in the sandbox reads the redirected registry and
  may not load its plug-ins. Closing the Terminal tab that started a Rhino ends that Rhino.
- Write the private client list (`%APPDATA%\PanelCladding\content-denylist.txt`) from the host.

## Yak channel

The alternative to the zip; Rhino's Package Manager owns install, update and loading.

- **Never publish to the public McNeel server**; the script refuses a `rhino3d.com` source before it
  builds. Publish only to a company-internal source the user names (`-Source`). A folder source
  gets the `.yak` copied in (`yak push` would ask for a Rhino Accounts login); an `http(s)` source
  gets `Yak.exe push` with that server's own authentication.
- **Versions are immutable**: different content under an existing file name is refused; bump.
- **One channel only**: zip and Yak must never coexist (`ID already in use`). Zip to Yak: run
  `Uninstall.cmd` first. `Install.cmd` on a Yak machine moves the Yak copy into `rollback\`.
- The `rh8_<n>` tag follows the build machine's RhinoCommon: built with Rhino 8.33, the package is
  offered only to Rhino 8.33 or newer.

```powershell
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorYak.ps1     # artifacts\panelcladdingeditor-<version>-rh8_<n>-win.yak + .sha256
.\tests\Package\Verify-YakPackage.ps1                                # contents, manifest, GUID, folder-source round trip
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorYak.ps1 -PackagePath <verified .yak> -Source <named source>
```

The Yak script reuses the zip build and its identity check. It needs Rhino 8's `Yak.exe`
(`%ProgramFiles%\Rhino 8\System\Yak.exe`; pass `-YakPath` for another location). Do not wrap it in
`*>&1` under Windows PowerShell 5.1, where Yak's "content version" warning then aborts the script.

## Release procedure

1. **Prepare** on a `feature/release-<version>` branch: bump the csproj `<Version>` and the
   manifest `version`; add `Packaging/PanelCladdingEditor/RELEASE-NOTES-<version>.md`,
   self-contained (install, upgrade notes, changes, validation) since older release pages are gone;
   update the package README for behaviour changes (it ships in the bundle). The root README links
   `releases/latest` and needs no edit.
2. **Verify** on the clean, committed source: `.\tests\Package\Verify-Release.ps1` (six release
   smokes, Debug/Release solution builds and direct RHP rebuilds, identity checks, package build,
   `Test-Package.ps1`, registration smoke) and `.\tests\Run-Regression.ps1`. `git status` stays clean.
3. **Check the version**: the packaged RHP's product version embeds its source commit
   (`<version>+<sha>`), so the published package must come from the commit you tag.
4. **Tag** that commit (annotated) and push the tag; merge the PR with a merge commit (not squash or
   rebase) so the tagged commit is in `master`.
5. **Publish** the archive `Verify-Release.ps1` produced, never a rebuild:

```powershell
$a = '.\Packaging\PanelCladdingEditor\artifacts'
[Diagnostics.FileVersionInfo]::GetVersionInfo("$a\PanelCladdingEditor-<version>\Plugin\PanelCladdingEditor.rhp").ProductVersion
git tag -a v<version> <verified-commit> -m 'PanelCladdingEditor <version>'; git push origin v<version>
gh release create v<version> --verify-tag --latest --title 'PanelCladdingEditor <version>' `
  --notes-file .\Packaging\PanelCladdingEditor\RELEASE-NOTES-<version>.md `
  "$a\PanelCladdingEditor-<version>-win-x64.zip" "$a\PanelCladdingEditor-<version>-win-x64.zip.sha256"
gh release view v<version> --json assets,isDraft,isPrerelease    # then download and compare SHA-256
```

6. **Check the release.** The asset hashes equal the local files, `releases/latest` is the new tag,
   the release is neither a draft nor a prerelease, and the remote tag equals the local one.
7. **Install and accept.** Install with host attestation (above), then run the live suites.

Never replace published assets; fix problems with a new version (`gh release list` shows the
current one).
Releases `v1.0.97`, `v1.2.5` and `v1.4.0` were withdrawn (deleted) because their bundled examples held
client project codes; their tags remain on the rewritten history, and they must not be republished.
Build and publish a Yak package only when the user asks and names a source.

## Live Rhino suites

See `tests/Live/README.md` for the full procedure. In short:

- **Prerequisites**: an installed, attested plug-in; Rhino 8 started from a host shell on a **copy**
  of a test model; MCP_Rhino installed (its live scripting runs `PcLive.cs`); an interactive
  desktop session. `web` needs Wireframe Web, `export` needs Excel, `otherdocs` a second Rhino.
- **Run**: `rhino_router_select_document`, then `preview_rhino_script` with
  `libraryPath = <repo>\tests\Live\PcLive.cs`, `libraryType = PcLive`,
  `script = "Result = PcLive.Start(doc, Parameters, Log);"`, `parametersJson = {"suite":"bake"}`
  (join suites with `+`; optional `version`, `project`, `mainModel`, `assembly`), then
  `apply_rhino_script` with the returned token. `Show-Results.py <evidenceDir>\<suite>.json`
  prints the results.
- `export` and `editor` need `Watch-Dialogs.ps1` (Windows UI Automation) and `Hold-Workbook.ps1`
  running in a host shell; under AGENTS.md, UI Automation needs an explicit user request.
- **Evidence** goes to `%LOCALAPPDATA%\PanelCladding\LiveEvidence\<yyMMdd>`; `PcLive` refuses a
  folder inside a git working tree. Commit only sanitized conclusions (suite, step names,
  pass/fail, counts) with fictional codes, never exports, screenshots or object/layer dumps, and
  delete the raw folder after the run.
- Suites change the test model (layouts, bake, lock, hide, move, duplicate-PID suffixes). Never
  call `RhinoDoc.Undo()` from a step; use the `_Undo` command.

## Repository and content guard

- **Remote.** `Island-Exterior-Fabricators/PanelCladding` (public), default branch `master`,
  `origin` `https://github.com/Island-Exterior-Fabricators/PanelCladding.git`. URLs of the earlier
  personal owners redirect. Repository settings need an organization owner.
- **Related repositories.** `Island-Exterior-Fabricators/MCP_Rhino` (private; the live-scripting
  host) and `Island-Exterior-Fabricators/wireframe-web` (PCweb's target).
- **Rewritten history.** On 2026-11-04 history was rewritten to remove client data. Delete and
  re-clone any older clone, and never push from one. GitHub pull-request refs and caches still reach
  the old objects; purging them needs an organization owner and GitHub Support.
- **Branches and records.** Construction branches are named `feature/<capability-name>`. The
  development records up to 2026-11-06 are kept at tag `records-2026-11-06`.
- **Content guard**: `pre-commit`, `commit-msg` and `pre-push` (LF sh wrappers) run
  `.githooks/Test-RepositoryContent.ps1` with `.githooks/content-rules.json` (project data file
  types, raw live-output folders, new evidence images, company project-folder paths) plus a private
  client list outside the repository (`%APPDATA%\PanelCladding\content-denylist.txt` or
  `PANELCLADDING_CONTENT_DENYLIST`; add every new client project; without it the guard warns and
  applies the other rules). Exit 0 clean, 1 blocked, 2 guard failure; check by hand with
  `-Mode Tracked` or `-Mode Commit -Rev <rev>`. Never bypass (`--no-verify`). Each clone and machine
  needs one regression run and its own copy of the list. The `repository-content-guard` smoke checks
  rules, wiring and hook behaviour.

## Known pitfalls

| Symptom | Cause | Fix |
| --- | --- | --- |
| PC commands unknown after an install, no error | Installer ran in a sandboxed shell: files moved, the real registry still points at the old RHP, now in `rollback\` | Close Rhino; run `Install.cmd` or `-Mode Repair` from a host shell; attest |
| Plug-in silently not loaded although the root timestamp is fresh | `PlugIn\FileName` is stale or dangling; Rhino never rewrites it | `-Mode Validate` ("registration is inconsistent"); `-Mode Repair`; attest |
| `ID already in use` | Zip and Yak (or any `packages\8.0` folder holding the RHP), a `.dll` twin, or a missing assembly `GuidAttribute` | One channel only; ship only the RHP; `Verify-PluginAssemblyIdentity.ps1` |
| Validate: "ownership manifest is missing or incompatible" | Bundle of another version | Validate from the installed version's bundle |
| Installer prints a `staged\...` path and stops | Rhino was running | Close every Rhino window; run the printed command |
| MSB3030, or the identity check finds no RHP | Parallel or solution builds left the test-host DLL | `-m:1`; rebuild the csproj with `-t:Rebuild` last |
| `Test-UserInstallation.ps1` refuses to run | Elevated token | Run package checks non-elevated |
| Release RHP names an older commit | Package built before the final commit | `Verify-Release.ps1` on the commit you tag |
| Yak build aborts on a "content version" warning | `*>&1` under Windows PowerShell 5.1 | Call it without the redirect |
| Yak publish: "Bump the version" | Version already in the source with other content | Bump the version |
| Commits are not checked | `core.hooksPath` unset (fresh clone, old worktree) | Run the regression once, or `git config core.hooksPath .githooks` |
| Guard says there is no private list | List written from the sandbox (`%APPDATA%` redirected) | Write or move it from a host shell |
| MCP_Rhino refuses every script after a live run | `RhinoDoc.Undo()` left an open undo record | Restart Rhino; steps use `_Undo` |
| Live Rhino lacks its plug-ins, or dialogs are not found | Rhino or `Watch-Dialogs.ps1` started in the sandbox | Start both from a host shell |
