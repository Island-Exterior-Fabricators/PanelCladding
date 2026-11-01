# PanelCladdingEditor package

This plug-in is independent of MCP_Rhino. Its product identity is
`PanelCladdingEditor`; build it with `Build-PanelCladdingEditorPackage.ps1`, then install or validate
it with `Install-PanelCladdingEditor.ps1`.

The Rhino command surface uses the `PC` prefix: `_PCeditor`, `_PCcreate`, `_PCclear`,
`_PCcrvTemplate`, `_PCmatchSrf`, `_PCmatchCrv`, `_PCbake`, `_PCsyncSrf`, `_PCsyncCrv`, `_PCpid`,
and `_PCtemplatePNL`. Since 1.3.0 every name is `PC`, the first word in lowercase and later words
capitalized (`PNL` stays an acronym). Rhino matches command names case-insensitively, so the former
spellings (`PCEditor`, `PCSyncCrv`, ...), aliases and toolbar macros still run.

`PCbake` (1.3.0) replaces `PCupdate`, `PCspawnSrf` and `PCspawnCrv`; aliases and macros that call
them must use `PCbake`. It treats the selected panels' saved attributes as authoritative and bakes
them into managed geometry. Its `Scope` option is `Surfaces` (the default on every run), `Curves` or
`All`; objects of a kind outside the scope are left as they are. Cladding surfaces are reconciled:
matching surfaces are rebuilt, missing ones are created, and stale or duplicate ones are deleted.
Extrusion curves are never overwritten, since they may carry hand-assigned components: a panel that
already has managed curves keeps all of them and is reported, and a panel without curves gets them.
Delete a panel's curves to bake new ones. `PCsyncSrf` and `PCsyncCrv` go the other way, reading
edited geometry back into the panel. `PCcrvTemplate`
assigns a full-run horizontal or vertical merge mask only to selected
panel Breps that do not already have a merge code. Configured panels in a mixed selection are
skipped and reported while the remaining eligible panels are updated. Selecting only configured
panels succeeds without changes. A one-row/one-column case with no applicable
run remains mask-free. Surface commands operate only on cladding Breps; curve commands operate only
on extrusion curves.

Since 1.1.0, panel attributes follow the project naming convention workbook
`<PROJECT> - Naming Convention.xlsx` (PNL sheet):

| Attribute | Value |
| --- | --- |
| `CW_1.06_UNIT_TYPE` | `C` Curved, `F` Flat, `L` Two Face, `Z` Three Face |
| `CW_1.08_FRAME_CONFIG` | code in the project's Frame-Config format (`AA`, `AB`, … by default; no `I`/`O`) |
| `CW_1.09_FRAME_TYPE` | hand-filled frame type (e.g. `oCornerL`); recorded in the PNL sheet's Frame-Type table, otherwise not read or written (1.2.5) |
| `CW_5.00_DEFINITION_A` | role: `parent`, `child`, `child 1`, `child 2` (lowercase since 1.2.5; any case is read) |
| `UD_CLADDING_LOGIC` | cell cladding JSON, e.g. `{"0A":"AL-01","0B":"0A","1A":""}` (ownership only before 1.2.4; `CW_9.01_CLADDING_LOGIC` in 1.1.0–1.2.1, `CW_2.13_CLADDING_LOGIC` before) |
| `UD_FRAME_CONFIG` | readable configuration, e.g. `2H 1V \| delete A1 0B \| merge B0–1 \| hide 0A` (`CW_9.02_FRAME_CONFIG` in 1.1.0–1.2.1) |
| `UD_FRAME_ASSIGNMENTS` | frame/extrusion assignment JSON (in `CW_1.09_FRAME_TYPE` before 1.2.5) |
| `UD_PLANE` | panel frame, an auto value: `{origin};{X axis};{Y axis}` (see below; `Plane` before 1.2.5 is not read) |

Since 1.2.5 the plug-in never overwrites or deletes the hand-assigned fields: `CW_1.09_FRAME_TYPE`,
`CW_1.10_*`, `CW_1.12_INSTALL_SEQUENCE`, `CW_2.06_PENETRATION_DIMENSION`, `CW_7.00_*`. PCpid adds a
blank placeholder only for a setup key that does not exist yet.

Since 1.2.2 the user-defined `CW_9.xx` series is `UD_XXX`. Panels that still carry
`CW_9.01`/`CW_9.02` are read as before, and the keys are renamed to `UD_` the next time a
cladding command writes the panel (`PCeditor` save, `PCcreate`, `PCmatchSrf`, `PCmatchCrv`,
`PCsyncSrf`, `PCcrvTemplate`, `PCbake`), in the same Undo step.

`UD_FRAME_CONFIG` is authoritative and lets the plug-in read a coded panel even when the workbook
is unavailable. `2H 1V` is the number of horizontal and vertical intermediates; horizontal
segments are track letter + bay number and vertical segments track number + bay letter,
as in the `INT-` curve codes. Codes are assigned per document when a configuration is first
written and are never reused; configurations with the same intermediate counts share the
first letter. The code table lives in the document string `PanelCladdingEditor.FrameConfigCodes`.
Only `L` and `Z` panels take a CID role suffix (`_P`, `_C`, `_C1`, `_C2`) from Definition A;
`C` and `F` panels are `parent` without a suffix. PCpid reserves Definition A, moving
existing definitions down (adding `E`, `F`, … as needed). The former JSON frame
configuration and `flat`/`corner_parent`/`corner_child` values must be migrated.

Since 1.2.3 the plug-in keeps these values current on every panel of
`<PROJECT>-IEF-Wireframe::Surfaces - PNL` and its sublayers (it replaces the AutoATT script; the
`AUTO_panel=1` mark is no longer needed and is left as it is). It is always on:

| Attribute | Value (90 × 305 in panel, `CW_6.01_PSF` = 85) |
| --- | --- |
| `CW_2.00_UNIT_DIMENSION` | `90.00000x305.00000` |
| `CW_2.01_UNIT_WIDTH` | `90.00000` (model units) |
| `CW_2.02_UNIT_HEIGHT` | `305.00000` (model units) |
| `CW_2.07_UNIT_AREA` | `190.625 sqf` (surface area in square feet) |
| `CW_6.02_UNIT_WEIGHT` | `16203.125` (`CW_6.01` × area; blank without a numeric `CW_6.01`) |
| `UD_PLANE` | `{x, y, z};{1,0,0};{0,0,1}` (since 1.2.5) |

`UD_PLANE` is the panel frame every command uses: Z points out of the facade (the front face
normal), Y is up and X is right seen from outside; the origin is the panel's lower-left corner.
Curved panels use their best-fit plane. A stored value that describes the same frame (origin
within the document tolerance, same axes) is kept as written.

Width and height are the same measurement `PCeditor` and `PCsyncSrf` use. Values refresh after a
panel is added, copied, edited or moved onto the branch, after `CW_6.01` changes, and for all
panels when a model opens (which marks the model modified if any value changed). The refresh runs
when no command is active and is not a separate Undo step; Undo restores the previous values.

Every save of the project's main model, `<PROJECT> - Wireframe.3dm` (the whole file name, any
case), updates the code tables in the PNL sheet. Since 1.2.1 other models of the project, such as
`MET - Wireframe_STN.3dm` or a copy like `BKT - Wireframe - v2.3dm`, never write the sheet: they
read its format and reserved Frame-Config codes, and print once per session that only the main
model updates it. Each table is found by its title in row 12
(`Unit-Type`, `Wall-Type`, `Frame-Config`, `Frame-Type`, matched ignoring case, spaces and
hyphens): the code is in the title's column, the description in the next one, and the rows start
at row 13. Missing titles are added in E, H and J; Frame-Type under its row-7 header (else L),
and only once there is a frame type to record. Wall-Type codes are single letters (`A`, `B`, …,
no `I`/`O`) for the `CW_1.07_WALL_TYPE` values. Since 1.2.5 the Frame-Type table lists the
`CW_1.09_FRAME_TYPE` values with codes in the project's Frame-Type format, the same way; a row
whose value is `NA` stands for panels with a blank frame type. Only these columns are written; header rows
and any other tables in the sheet are left as they are, and nothing is written when the
tables are already current. Since 1.1.1 the PNL header order is Unit-Type | Wall-Type |
Frame-Config | Frame-Type | Cladding | Definition | Seq (1.1.0 wrote E/F, I/J and W/X from row 12).
Since 1.1.3 the save also writes the project code into the example-row cells under the row-7
`Job Code` header, one letter per cell (or whole in a single cell); a code that does not fit is
reported once and the cells are left as they are.
Codes no panel uses are kept, struck through and greyed. The workbook is found next to the
model, or at the document string `PanelCladdingEditor.NamingConventionPath`. If it is open or
locked the model still saves, a warning is printed, and the next save retries. The plug-in
does not generate `CW_1.00_PNL`.

Since 1.2.0 each project records its PNL code format with `PCtemplatePNL`. The command asks for
the Wall-Type, Frame-Config, Frame-Type and Cladding formats one at a time; each answer is
required and is one of `AA` (two letters), `A` (one letter), `A1` (letter + digit), `01` (two
digits) or `001` (three digits). Letters `I` and `O` are never used. Definition is always `01`
and, like every two-digit segment, continues `01`..`99`, `A1`..`Z9`, `AA`..`ZZ`. A format that
codes already written in the sheet do not fit is refused, because codes never move. The command
then lays out the PNL sheet: the row-7 segments (Wall-Type as wide as its code, the others at
least two columns; a segment that is too narrow gains columns at its end and everything to its
right moves), the example row 8 (one character per cell, merged across the segment for a
one-character code), the row-12 titles and 24 placeholder codes under each table (none for
grouped `AA`/`A1` frame configurations). The format is stored in the workbook's custom property
`PanelCladdingEditor.PNLFormat`, so every model of the project shares it; each model also keeps a
copy in the document string of the same name. Close the workbook in Excel first.
For MET the answers are `A`, `A`, `01`, `001`; BKT, which has no recorded format, keeps `A`,
`AA`, `01`, `01`, `01`.

Since 1.2.0 the save-time sync follows the recorded format and changes the sheet in place: a code
keeps its row, a placeholder (code without value) is filled when a new value takes that code, a
written value is never replaced (a different model value is reported), only the struck state of
used/unused rows changes, and new codes go below the table. Placeholder rows, other rows, merges
and number cells are kept. `CW_1.08` follows the format on every panel write; without a recorded
format a code of another shape (such as MET's one-letter `A`) is left as it is.

Since 1.0.99, identifiers use `-` between the tokens of one ID and `_` between a panel
CID and its role or component: `PID-BKT-N2-05-05`, panel `CID-BKT-N2-05-05_C`, cladding
surface `CID-BKT-N2-05-05_C_0A`, and extrusion curve `CID-BKT-N2-05-05_C_FRM-0`. Curve
codes (`CRV`) also use `-`: `FRM-0`…`FRM-3`, `INT-A0`, `INT-0A`, merged `INT-A0–1`, and
`UD_FRAME_ASSIGNMENTS` keys perimeter frames as `FRM-n`. Object names follow the CID
(`N2-05-05_C`). The former `PID_BKT_N2_05_05` / `CID_BKT_N2_05_05-C` / `FRM_0` forms
are no longer produced or matched; migrate existing documents before using 1.0.99.

Since 1.1.3 the project code comes from the Rhino file name: the text before the first ` - `,
up to its first non-alphanumeric character, so `BKT - Wireframe.3dm` is `BKT`. It names the only
project root layer, `BKT-IEF-Wireframe`; panels and curves under another `*-IEF-Wireframe` root are
outside the project. The same code is the naming-convention workbook's name and Job Code, and
PCpid offers it as the default project code. Unsaved documents accept any `*-IEF-Wireframe` root.

Since 1.0.98, the plug-in follows the project layer standard. `<PROJECT>` is the
project code of the top-level `<PROJECT>-IEF-Wireframe` layer, for example `BKT`:

| Objects | Layer |
| --- | --- |
| Source panels | `<PROJECT>-IEF-Wireframe::Surfaces - PNL::Surfaces - <type>` |
| Extrusion curves | `<PROJECT>-IEF-Wireframe::Curves - PNL::Curves - <type>` |
| Cladding surfaces | `Reference::Surfaces - STEP::Surfaces - <prefix>::<material>` |

A panel on `Surfaces - WT_01` gets its curves on `Curves - WT_01` under the same
project root. The cladding family layer is the material code's leading letters, so
`GLS-001` goes to `Surfaces - GLS::GLS-001` and `MPL-002` to `Surfaces - MPL::MPL-002`.
Missing layers are created by `PCbake`. Only these two output trees are treated
as managed dependencies; copies elsewhere (for example the wireframe
`Surfaces - GLS` glass) are not discovered, updated or deleted by bake or sync.
The former `01_CW Panels`, `02_CW Extrusions`, `04_STEP Surfaces` and
`03_Material Surfaces (STEP)` trees are no longer recognized; re-layer older documents first.

Since 1.3.0, layers whose name contains `Not in Scope` or `By Others` (any case; spaces, `-` and
`_` ignored), at any depth under `Surfaces - PNL` or `Curves - PNL`, are not tracked: PCpid does
not number their panels, auto attributes and the naming sync skip them, `PCbake` does not create
curves for them or treat curves there as managed, and `PCreport` leaves them out. Values already on
those objects stay as they are.

Since 1.0.96, `PCmatchCrv` copies extrusion assignments together with the segment, merge and hide
masks. Select target panel Breps, then the source; horizontal/vertical grid counts
must match. Profiles, quantities, formulas, parent links and length modifiers are
replaced by the source assignments, while target dimensions, offsets and cladding
remain intact. Configuration is stored in `CW_1.08_FRAME_CONFIG`, and profile
assignments in `UD_FRAME_ASSIGNMENTS` (`CW_1.09_FRAME_TYPE` before 1.2.5). An unassigned source
clears target assignments. Run `PCbake` with `Scope=Curves` to generate curves for panels that have none;
existing curves are kept, so delete them first to bake curves from the matched settings.

Since 1.0.95, Extrusion Setup includes Clear all beside Cancel. After a Yes/No
confirmation it clears configured and unconfigured profiles, editor values and
the schedule PDF path. Confirm saves the cleared catalogue; Cancel discards it.
The project workbook remains selected so a new schedule can be extracted.

Since 1.0.94, Material Setup includes a trash button beside the project material
catalogue. Select a material to remove it from the working catalogue, then choose
Confirm to save or Cancel to discard the change. Existing panel assignments are
preserved.

Since 1.0.93, PCpid measures the outer boundary on Rhino's verified face plane.
It no longer treats bounding-box thickness as nonplanarity; actual nonplanar
surfaces are still rejected at the document tolerance.

`PCpid` (1.0.89) is the first setup step, before PCcreate. In a saved document it
prompts for a three-letter project code, the panels to update,
a north-facade panel, and a first-floor panel. It writes
`CW_1.03_ELEVATION`, `CW_1.04_LEVEL`, `CW_1.01_PID` and `CW_1.02_CID`, for example
`PID-BKT-W3-05-11` and `CID-BKT-W3-05-11`. CID keeps the existing unit-type role
suffix policy, and the panel name follows the existing shortened-CID convention.
Bay is the last PID/CID segment; PCpid does not introduce another user-text key.

Since 1.0.90, numbering considers every surface/Brep on
`<PROJECT>-IEF-Wireframe::Surfaces - PNL` (before 1.0.98, `01_CW Panels::Surfaces-PNL`)
and every nested sublayer, including hidden, locked
and reference panels as read-only context. Only selected editable panels in this
subtree receive PID/CID/elevation/level/name changes. North and first-floor
references may be unselected panels in the subtree. Unrelated layers and similarly
named sibling layers are excluded from numbering.

Since 1.0.91, PCpid also ensures all 30 panel setup keys requested for CW_1.00–1.11,
CW_2.00–2.02, the four CW_2.05 anchor fields, CW_2.06 penetration, the four CW_5.00
definitions, CW_6.00 wind loads, CW_6.01 PSF, CW_6.02 weight and CW_7.00 blinds
exist on selected panels. Existing non-generated values are retained; unassigned
fields use a stored space so Rhino retains the key. Width and height come from
each panel's tight bounds in its own facade plane, in model units with five decimal
places. Unit dimension is `widthxheight`. These three dimensions are refreshed
alongside the IDs on every run. Conflicting case variants fail before any writes.

Since 1.0.92, both PCpid and PCbake also standardize selected source surfaces
using the MCP standard four-point skill's convention: lower-left anchor viewed
from the oriented front, clockwise corners in a gravity-aligned local frame,
four-point rebuild, front/back flip, then SwapUV. The original front direction,
footprint, object ID and metadata are retained. Already-standardized surfaces are
not replaced again. Geometry changes share the owning command's Undo record.
PCbake generates dependencies after this preparation and continues to exclude
duplicate-CID sources. PCpid's unselected context surfaces are never reordered.

Point ordering supports planar, straight, convex four-corner surfaces without
holes. Unsupported or ambiguous surfaces are reported as skipped for point order;
their geometry is retained and the command's other supported operations continue.
Horizontal faces have no gravity-defined lower-left and are also reported as skipped
by the ordering step (PCpid still requires vertical facade geometry overall).

The panels must be vertical planar single-face surfaces/Breps with outward normals.
The north reference defines project north, including for rotated buildings; world
Z defines up. Same-facing panels on different planes receive separate elevation
numbers, ordered left-to-right then bottom-to-top when viewed from outside.
Aligned bottom edges identify levels across the full panel subtree; the picked floor is 01
and observed rows increment above it (00, -01, etc. below it). Aligned left edges
identify bays, restarting at 01 for every elevation. All alignment uses document
tolerance. Unequal panel heights/widths are supported when their starting edges
align. Missing floors/bays are not inferred from gaps, and staggered layouts may
require preparation. Temporary IDs, existing elevation text, and object names do
not drive numbering. Updating a single panel retains the same inferred address as
updating the complete panel subtree with the same references.

All geometry and document-wide identity checks run before any attribute write.
Duplicate addresses and collisions with unselected objects fail without changes.
The PID audit checks the resulting state of all panels in the subtree: selected
proposed IDs plus unselected existing IDs. Even duplicate PIDs on two unselected
panels are reported and block writes. Blank unselected IDs stay blank; selected
updates that resolve old duplicates are allowed.
Renumbering a panel that already has generated dependencies is blocked until those
dependencies are cleared. An identical rerun makes no changes. Attribute changes
share one Rhino Undo entry; unrelated metadata and geometry are preserved. Escape
at any of the four prompts exits before attributes are written.

`PCcreate` initializes the selected panels' grid from selected guide curves. Since
version 1.0.80 it also writes `CW_2.00_UNIT_DIMENSION`, `CW_2.01_UNIT_WIDTH`, and
`CW_2.02_UNIT_HEIGHT` from each panel's local extents, using invariant five-decimal
values (`widthxheight` for the combined dimension). Existing dimension values are
refreshed. Rerunning PCcreate resets the generated grid; since 1.2.4 it no longer writes or deletes cell
assignments (see *Cladding configuration* below).
Since version 1.0.81, its intermediate tracks start segmented even when a guide
spans multiple cells. Guide coverage still determines missing segments; guide
continuity does not create a merge mask. Merge explicitly in PCeditor or apply
PCcrvTemplate when desired. Perimeter frame behavior is unchanged.

Since version 1.0.82, changing H/V values or row/column dimensions in PCeditor
preserves the current delete, hide, and merge masks by track/segment index.
Cladding materials, parent-cell links, and extrusion profile assignments retain
their layout while coordinates move. Save/reload persists the same masks; adding
or removing tracks remains an explicit topology edit.

Since version 1.0.84, cladding-view dashed boundaries follow the resolved cladding
region across actual shared edges, including indirect parent references and
spanning/nonrectangular logical cells. Cell label numbering and material code
equality do not determine line style: separate owners stay solid even with the
same material, and deleted logical-cell interiors remain absent.

Use `PCsyncSrf` after editing associated cladding surfaces or their material layers
to reconstruct the selected panels' cladding layout, material assignments, and
cell ownership. Use `PCsyncCrv` after editing associated extrusion curves to
reconstruct the selected panels' H/V grid and curve topology. Both read existing
geometry back into panel attributes and refresh dependency metadata and panel
dimensions; select the source panel Breps in a saved document.

`PCbake` participates in Rhino's command-owned Undo record when invoked from the command line and
opens its own record only when called without an active command record. A successful batch therefore
appears as one normal Rhino Undo entry without attempting an unsupported nested record.
Since version 1.0.83, PCbake normalizes parent/child CIDs before checking selected
panel identities. Parent and child panels sharing a PID can update together.
Every panel in a duplicate-CID group is skipped; unique-CID panels still update,
and skipped panels remain selected with their duplicate CIDs reported. An
all-duplicate selection succeeds without dependency changes. Legacy unsuffixed
dependencies whose ownership is ambiguous between selected panels are preserved
and reported; dependencies belonging to skipped panels are excluded from updates.
Existing managed dependencies can remain object-locked, object-hidden, or on locked/hidden managed
layers: update and stale/duplicate deletion bypass those mutation modes without unlocking/showing
the objects or changing layer state, and retained object-level mode is preserved.
Managed extrusion curves use object colors by planned type: main-frame curves are Blue
(`RGB 0,0,255`), horizontal intermediate curves are Purple (`RGB 128,0,128`), and vertical
intermediate curves are DarkGreen (`RGB 0,100,0`). `PCbake` applies the colors to the curves it creates,
and `PCsyncCrv` corrects existing curves within the selected-panel scope.

Panel cladding saves persist cell ownership and materials under
`UD_CLADDING_LOGIC` (`CW_9.01_CLADDING_LOGIC` in 1.1.0–1.2.1, `CW_2.13_CLADDING_LOGIC` before).
This key stores the cladding JSON, separate from the hidden segments in `UD_FRAME_CONFIG`.

### Cladding configuration (1.2.4)

`UD_CLADDING_LOGIC` is the only cladding the plug-in reads. Each cell holds its material code
(owner cell), its owner's label (member cell) or `""` (unassigned). The `CW_4.xx_CLADDING_*` keys
are still written on every cladding save, as output for schedules and scripts, but are never read:
their number or content cannot stop a panel from loading.

- Unconfigured panel (no `UD_CLADDING_LOGIC`): it loads from the `CW_2.03`/`CW_2.04` offsets
  alone, with blank cells. `PCcreate` writes no `CW_4` placeholders or logic and leaves any
  `CW_4` keys already on the panel untouched; `PCsyncCrv` leaves its cladding keys alone;
  `PCmatchSrf` refuses it as a source.
- Configured panel: the logic must name exactly the cells of the H/V grid
  (`PANEL_CLADDING_LOGIC_GRID_MISMATCH` otherwise), and `UD_FRAME_CONFIG` must fit H/V as before.
  Rerunning `PCcreate` keeps the logic when the grid size is unchanged and deletes it when the
  grid size changed. In `PCeditor`, a grid-size change on a configured panel must be saved with
  Save Both.
- Logic saved before 1.2.4 holds ownership only; such a panel takes its owner materials from its
  `CW_4` keys once, and the next cladding write stores them in the logic. `PCsyncSrf` combines that saved owner graph with current Rhino surface
coverage and material layers, while edited splits and merges remain geometry-authoritative.

Since version 1.0.85, panel roles come from `CW_1.06_UNIT_TYPE` (since 1.1.0 together
with `CW_5.00_DEFINITION_A`, see above; before 1.1.0 `flat` had no suffix, `corner_parent`
used `_P` and `corner_child` `_C`, and `-P`/`-C` before 1.0.99). Keys and values ignore case and surrounding value
whitespace. The old `parent` and `child` flags are ignored. Material surfaces and
extrusion curves inherit the suffix before their cell/curve code, for example
`CID-BKT-W1-03-01_P_0A` and `CID-BKT-W1-03-01_P_INT-B1`.
Existing PC save/create/match/spawn/sync/update paths apply the rule. Changing to
`flat` clears a stale panel CID role suffix. A missing or unsupported unit type
does not rewrite an existing custom panel CID or fall back to legacy flags.

Since version 1.0.86, panel and managed dependency object names use a shortened
CID: `CID-BKT-S1-06-14` becomes `S1-06-14`, and
`CID-BKT-S1-06-14_P_INT-B1` becomes `S1-06-14_P_INT-B1`. Since 1.2.5 the leading
`CID-<project>-` of any project is removed (`CID-MET-S-04-36` becomes `S-04-36`), without
changing the full CID user text; a `CID-` identifier without a second `-` loses only `CID-`,
and custom identifiers are preserved. Prefix matching ignores case.
Create, save, match, bake, and sync normalize source-panel names from the
current CID. Bake and sync apply the same rule to surfaces and extrusion curves.
Run PCbake on existing configured panels to refresh their surface names; existing curves keep
theirs until `PCsyncCrv` runs.
Both sync commands detect name-only changes; missing CID leaves the panel name alone.

Since version 1.0.87, PCeditor displays total width and height on separate lines
with five decimal places and shows the shortened panel CID in Current Panel.
In Extrusion view, selecting frame or intermediate curves opens the floating
profile-assignment panel on the canvas. Drag profiles from the sidebar into it;
clear the selection or press Escape to hide it.
Version 1.0.88 fits assigned profiles into two columns within the same floating
panel width, with each profile code above its length modifier and remove button.

Curve profile assignment is suspended. PCeditor's Extrusion view edits the layout only
(merge, explode, hide, add, delete and offsets); it no longer shows the profile catalogue, the
floating assignment panel or assigned codes on curve labels. Existing `UD_FRAME_ASSIGNMENTS`
are kept, follow layout edits and are saved back; PCmatchCrv, PCsyncCrv and PCbake (for curves it
creates) still use them. The assignment UI remains dormant for a later decision.

Baked cladding surfaces and extrusion curves inherit the panel's `CW_1.05_LOT`
value, including leading zeros. `PCbake`, `PCsyncSrf`, and `PCsyncCrv` use this
canonical lot key for inherited metadata. An absent panel lot leaves synced
dependencies without that key; baking surfaces requires a populated lot.
Version 1.0.79 replaces `CW_1.05_RELEASE` with `CW_1.05_LOT` without an old-key
fallback. Existing Rhino data must already use the new name.

Every baked cladding Brep persists its complete logical coverage under
`Merge_Mark`, including the owner: a single-cell surface stores `0A`, while a merged
surface stores values such as `0A;1A`. A complete baked coverage partition lets `PCsyncSrf`
distinguish a hidden mullion inside one merged surface from an obsolete boundary between separate
surfaces. Unmarked surface sets do not preserve geometry-missing offsets from the PCeditor parent
graph; unmatched stored values are cleared and the geometry-derived canonical coverage is then
written. Mixed or invalid coverage fails before mutation rather than guessing. The former
`CW_1.03_CLADDING_CELLS` name is accepted only for migration: sync removes it and writes
`Merge_Mark`; conflicting dual values fail before mutation.

The plug-in no longer writes the panel user-text keys `Signature` or
`CW_4.00_CLADDING_SIGNATURE`. Touched panels remove either legacy value; layout compatibility and
reconciliation use the authoritative `CW_2.10`-`CW_2.13` topology/logic plus `CW_4.xx` material and
parent assignments directly.

Combined cladding types are suspended. Editor saves and surface/curve sync no longer
calculate or persist `CW_2.14_CLADDING_TYPE`, and the editor no longer shows a cladding
type preview. Every save scope and changed-panel sync removes that attribute and its
previous names (`CW_2.13_CLADDING_TYPE`, `CW_1.10_CLADDING_TYPE`, `CW_4.00_CLADDING_TYPE`).
PCclear also recognizes these retired keys. Material, owner-graph, and topology
validation remain active. The former frame-typology identifier is also retired.
The old identity/workbook utilities remain dormant for a future design decision.

From 1.0.96 to 1.0.99, frame configuration was stored as versioned JSON in
`CW_1.08_FRAME_CONFIG`: `{"v":1,"delete":"<mask>","merge":"<mask>","hide":"<mask>"}`.
Since 1.1.0 it is the readable `UD_FRAME_CONFIG` text (`CW_9.02_FRAME_CONFIG` before 1.2.2)
plus the `CW_1.08` code; the JSON
form is still read and is converted the next time the panel is written.
Each field retains the existing mask encoding and grid dimensions, including the
delete mask's original polarity. A saved default grid also gets a complete config.
No dimensions, profile definitions or generated typology hash are included in this value.

`UD_FRAME_ASSIGNMENTS` (`CW_1.09_FRAME_TYPE` before 1.2.5; such old payloads are no longer read)
stores the existing version-2 assignment JSON unchanged: `f`
contains perimeter assignments, `s` segment assignments, `d` profile definitions
and calculation settings, and `x` length modifiers. No assigned profiles means
this key is removed. Generated curve attributes (`Extrusions` and per-code formulas)
keep their existing format.

Existing `CW_2.12_DELETE_MASK`, `CW_2.10_MERGE_MASK`, `CW_2.11_HIDE_MASK` and
`CW_2.09_FRAME_ASSIGNMENTS` remain readable for migration. Nonblank new attributes
take precedence; blank PCpid placeholders allow legacy fallback. Malformed new
payloads fail validation. Saving extrusions/both or matching curve settings writes
the new keys and removes the old masks, assignment key and `CW_1.5D_FRAME TYPOLOGY`.
PCcreate resets frame settings under the new keys; changed template/sync operations
also use the new format. Cladding-only saves preserve frame settings.

Version 1.0.78 renames the former `CW_2.05_SEGMENT_MASK`, `CW_2.06_MERGE_MASK`, `CW_2.07_HIDE_MASK`,
and `CW_2.08_CLADDING_LOGIC` keys to the 2.12/2.10/2.11/2.13 names and suspends cladding type metadata.
Documents predating that rename still require that earlier mask/logic migration.

The extrusion view maintains an image-backed project catalogue of framing profiles in the workbook
`Extrusions` sheet. PDF schedule die numbers such as `ALU-H0651` import as `1D-ALU-H0651`; profile
codes are additive per `FRM`/`INT` curve and baked curves expose the sorted codes in `Extrusions`
user text for length take-off. Panel assignment state is stored in `UD_FRAME_ASSIGNMENTS`.

The package keeps the Windows x64 Skia/HarfBuzz native binaries both in their `.deps.json` runtime
paths and beside the RHP. Rhino's plug-in load context requires the top-level copies for native
P/Invoke resolution during PDF profile extraction.

When Rhino is open, the production installer stages the bundle under LocalAppData instead of
overwriting a loaded plug-in. Close Rhino and run the installer inside the reported staged bundle to
activate it.

The installed RHP and its private dependencies live at
`%LOCALAPPDATA%\PanelCladdingEditor\plugin\<version>`. The installer writes the complete Rhino 8
current-user registration directly: root product/startup metadata, `PlugIn\FileName`, and the exact
thirteen-command `CommandList`. It does not rely on Rhino consuming a shorthand root `FileName` during
first startup. Installer validation rejects shorthand-only, partial, stale-command, or mixed
registrations. It removes the legacy `PanelCladdingEditor` and `BayHealthPanelCladdingEditor` roots
from Rhino Package Manager discovery. Do not place the RHP under
`%APPDATA%\McNeel\Rhinoceros\packages` while the registry installation exists; doing so gives
Rhino two loaders for one plug-in identity and produces `ID already in use`. To move to the Yak
package, uninstall this installation first (see Package Manager installation).

Only `PanelCladdingEditor.rhp` is shipped for the plug-in assembly. Do not also ship the identical
`.dll`; Rhino interprets both files as plug-ins with the same ID.

The installer writes `%LOCALAPPDATA%\PanelCladdingEditor\install-manifest.json` for hash validation
and ownership-aware uninstall. Run `Uninstall-PanelCladdingEditor.ps1` only while Rhino is closed.

## Package Manager (Yak) installation

Since 1.2.4 the plug-in is also published as a Rhino Yak package, so users install and update it
from Rhino's `_PackageManager` instead of running the zip installer. The package goes to a
company-internal package source, not the public McNeel server: a shared folder (or a private Yak
server) that Package Manager lists alongside `https://yak.rhino3d.com`. Only people with access to
that folder see the package.

Build, and optionally publish, from the repository:

```powershell
# Build artifacts\panelcladdingeditor-<version>-rh8_<n>-win.yak and its .sha256
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorYak.ps1
# Build and copy the package into the company source folder
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorYak.ps1 -Source \\server\share\RhinoPackages
# Publish a package that is already built and verified
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorYak.ps1 -PackagePath <file.yak> -Source \\server\share\RhinoPackages
```

The script reuses `Build-PanelCladdingEditorPackage.ps1` (same files and GUID checks), writes the
Yak `manifest.yml` with a `guid:` keyword so Rhino can offer the package when a model needs the
plug-in, and runs Rhino 8's `Yak.exe build --platform win`. A folder source only needs the `.yak`
file copied in, so publishing needs no Rhino Accounts login; an `http(s)` source is pushed with
`Yak.exe push` and that server's own authentication. The public McNeel server is refused. A
published version is never replaced: publishing different content under an existing version
fails, so bump the version for every release.

The `rh8_<n>` tag follows the RhinoCommon the plug-in was compiled against: a package built with
Rhino 8.33 installed is offered only to Rhino 8.33 or newer.

Each user, once:

1. Close Rhino. If the zip release is installed, run its `Uninstall.cmd` (the Yak copy and the
   registry copy must never both exist; the zip installer also moves a Package Manager copy aside).
2. Start Rhino, open `Options > Package Manager`, and add the company folder (for example
   `\\server\share\RhinoPackages`) to `Sources`.
3. Run `_PackageManager`, search `PanelCladdingEditor`, install, and restart Rhino.

Updating: publish a new version to the source; users see it in `_PackageManager` (Updates) and
the new version loads after a Rhino restart. Rhino installs packages under
`%APPDATA%\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\<version>`.

## Downloadable release installation

Download the `PanelCladdingEditor-<version>-win-x64.zip` asset from
[GitHub Releases](https://github.com/nuofan-island/PanelCladding/releases/latest), extract
all files, close Rhino, and run `Install.cmd` normally. The launcher installs for
the current Windows user and runs `-Mode Validate` before reporting success.
Restart Rhino and run `PCeditor`. Use `Uninstall.cmd` with Rhino closed to remove it.
See `QUICKSTART.txt` for command-line repair, validation and checksum verification.

Installation uses LocalAppData and HKCU only. It requires no administrator rights,
SDK, MCP server, or machine-wide execution-policy changes. Rhino 8 with its .NET 8
runtime must already be installed on Windows x64; macOS is not supported by the WPF
UI. Organization script policies may still restrict unsigned PowerShell scripts.

Rhino 8.20 or newer includes .NET 8 by default. If Rhino is configured to use
.NET Framework, select .NET Core with `SetDotNetRuntime` and restart it. See
[McNeel's runtime guide](https://developer.rhino3d.com/guides/rhinocommon/moving-to-dotnet-core/).

The package builder verifies compiled assembly/class/manifest GUIDs and versions,
then produces a ZIP and SHA-256 sidecar beside the bundle directory. Only the direct
RHP and private managed/Windows x64 native dependencies are shipped. Build machines
require the .NET 8 SDK and RhinoCommon; end users do not need the SDK.
