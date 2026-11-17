# PanelCladding tests

All rerunnable test code lives here. The repository is public: tests and fixtures hold no client
project data, and renders and raw live-run output stay out of the repository (see `.githooks`).

| Folder | Contents |
| --- | --- |
| `Smoke/<name>/` | Managed smoke projects, one per capability, with their input fixtures. `<name>` is the capability name of the change that introduced it. |
| `Shared/` | Code compiled into smokes: `RegressionPaths.cs` (all smokes) and `CladdingLogicFixture.cs` (linked by the projects that need it). |
| `Package/` | Package, installer, registration, identity and Yak checks against a built bundle. |
| `Checks/` | Static checks that need no build. |
| `Probes/` | Manual probes that take arguments, such as an installed bundle path. |
| `Live/` | Live acceptance checks of a release's manual test list, run inside Rhino against the installed plug-in on a test model, with helpers that answer its dialogs and hold Excel workbooks. Not run by `Run-Regression.ps1`: they need the installed RHP, Rhino and a desktop session. See [`Live/README.md`](Live/README.md). |
| `Run-Regression.ps1` | Builds and runs the smokes. |

## Running

```powershell
.\tests\Run-Regression.ps1                                   # every smoke, Debug and Release
.\tests\Run-Regression.ps1 -Configuration Debug -Name panel-cladding-pid*
.\tests\Run-Regression.ps1 -List
.\tests\Package\Test-Package.ps1 -Archive .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-<version>-win-x64.zip
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
.\tests\Package\Verify-Release.ps1                          # release smokes, builds, package, registration
```

`Run-Regression.ps1` builds each smoke serially, runs it from the repository root with stdin
closed, and writes one log per smoke to `artifacts/tests/logs/<Configuration>/` and a summary to
`artifacts/tests/regression-<Configuration>.json`. It exits non-zero if any smoke fails. Every
smoke must pass; there is no list of accepted failures.

Smokes build into the shared `artifacts/tests/bin/<Configuration>/` folder
(`Smoke/Directory.Build.props`). Each one references `src` with `PanelCladdingTestHost=true`, so
do not run smoke builds in parallel with each other or with a production RHP build. Rebuild the
production project before packaging (the package script does).

Parts that need a Rhino host or native initialization print `[SKIP]` in a plain run; a build or
managed run does not prove live acceptance.

## Adding a smoke

1. Create `Smoke/<capability-name>/` with one `.csproj` (copy a neighbour) and `Program.cs`.
   Reference `..\..\..\src\PanelCladdingEditor\PanelCladdingEditor.csproj` with
   `AdditionalProperties="PanelCladdingTestHost=true"`.
2. Return exit code 0 only when every check passed.
3. Read repository files through `RegressionPaths.RepositoryRoot`. Write renders and other
   generated files only to `RegressionPaths.Output("<capability-name>")`
   (`artifacts/tests/<capability-name>/`), never into a tracked folder.
4. Describe what generated files showed as text in the pull request description; renders and
   other images stay in `artifacts/`.
5. UI smokes lay out WPF views off-screen at the workstation's display scaling. Views with
   `UseLayoutRounding` snap sizes to whole device pixels (a 79-DIP width arranges as 79.2 at
   125%), so compare an `ActualWidth` or `ActualHeight` with the layout-rounded value
   (`Math.Round(value * scale) / scale`, `scale` from `VisualTreeHelper.GetDpi(element)`), never
   with the raw DIP value.
6. When later work changes behaviour on purpose, update the affected smokes in the same change.

Where the test code of the former `Project_Test/` folders went is listed in
`Project_Archive/Project_Test/261028_TEST_regression-test-layout/folder-map.md` at tag
`records-2026-11-06`.
