# PCpid planar bounds regression

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-pid-planar-bounds`](../../tests/Smoke/panel-cladding-pid-planar-bounds/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-pid-planar-bounds`); `Verify-Regression.ps1` is replaced by `.\tests\Run-Regression.ps1 -Name panel-cladding-pid,panel-cladding-pid-layer-scope,panel-cladding-pid-setup-keys,panel-cladding-point-order,panel-cladding-pid-planar-bounds`. Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

`reported-panel.json` contains the numeric boundary and normal from read-only live
inspection, with document path, project metadata and object identity removed.
Rhino reported a planar degree-1 2x2 surface; document tolerance is 0.00001.
Its dimensions are 90 by 180 model units. The old planner error proves the
adapter's box corners failed its plane check; the exact native box inflation or
cache behavior could not be inspected through the available read tools.

Run the five related suites plus serialized Debug/Release solution and standalone
RHP builds with `./Verify-Regression.ps1`. Logs are written in this directory.
The focused managed suite checks the reported boundary's plan, dimensions,
30-key setup, point order and retained nonplanarity guard. Existing suites cover
all 20 tutorial IDs, rotations/translations, subset numbering, whole-scope
uniqueness, metadata casing, dimensions and point-order permutations.

Native adapter assertions are in `NativeGeometrySmoke.cs`:

```powershell
$env:PATH = 'C:\Program Files\Rhino 8\System;' + $env:PATH
dotnet Project_Test/261007_TEST_panel-cladding-pid-planar-bounds/bin/Debug/net8.0-windows/PanelCladdingPidPlanarBoundsSmoke.dll --geometry
```

The geometry-only attempt on 2026-10-07 failed before the first native assertion:
`DllNotFoundException`, rhcommon_c DLL initialization failed (`0x8007045A`),
at `RHC_RhinoCreateSurfaceFromCorners`. Exit code 1. The prior RhinoCore host
also could not initialize. Native measurement, trimmed/curved boundary and warp
rejection tests compile but remain unexecuted. No user geometry was changed.

Native test coverage includes 12 rotation/front/UV variants, a trimmed rectangle,
a circle, nonplanar and horizontal rejection, front preservation and read-only
measurement. Interactive PCpid/Undo acceptance remains pending in Rhino.

1.0.93 package identity and all 27 bundle hashes passed; see package-summary.json.
Installed while Rhino was closed, with pre-activation independent host nonce
attestation and separate post-exit host Validate. Verify-Activation.ps1 passed
the exact 12 commands, new existing RHP path/hash, all three advanced key times,
independent registry-provider agreement and the prior 1.0.92 rollback hash.
See activation-summary.json. Loaded-module/post-start checks await Rhino reopening.
