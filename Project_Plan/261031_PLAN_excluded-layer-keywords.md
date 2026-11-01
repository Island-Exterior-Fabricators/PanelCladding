# Excluded layer keywords PLAN

## Background and authorization

On 2026-10-31, while planning the compliance report, the user decided that layers named with
keywords such as "Not in Scope" or "By Others" need no tracking at all. Today
`Surfaces - Not in Scope` sits inside `Surfaces - PNL`, so its panels are numbered by PCpid,
refreshed by the auto attributes, counted by the naming sync and routed to
`Curves - Not in Scope` (noted as a possible rule in the 1.0.98 layer-standard EXET). This request
authorizes the construction change and its PLAN/EXET/TEST records; not packaging or installation.

## Goals

- Any layer path with a segment whose name contains an excluded keyword is outside the panel and
  curve scopes, wherever those scopes are used.
- Keywords: `Not in Scope`, `By Others`; matching ignores case, spaces, `-` and `_`.
- Values already on excluded panels are left as they are.

## Architecture ownership

Application only. `PanelCladdingExtrusionPlanningService` owns the panel and curve scope
predicates; it gains `ExcludedLayerKeywords` and `IsExcludedLayerPath`, and its predicates use
them. Every adapter already routes through those predicates, so no Rhino adapter changes.

## Design

- `IsPanelLayerPath` and `IsCurveLayerPath` return false for an excluded path. This removes such
  panels from PCpid numbering and context, auto attributes, the naming-convention sync and (next)
  the report, and curves on such layers from PCbake's managed-curve discovery.
- `ResolveCurveLayerPath` fails with `PANEL_CLADDING_PANEL_LAYER_EXCLUDED`, so PCbake does not
  create curves for an excluded panel. Commands that act on an explicitly selected panel
  (PCeditor, PCbake surfaces) treat it like a panel outside `Surfaces - PNL`, as today.
- Version stays 1.3.0 (same release).

## Affected files

`Application/Services/PanelCladding/PanelCladdingExtrusionPlanningService.cs`; package README
(layer standard section); smokes whose fixtures count `Surfaces - Not in Scope` as in scope; new
smoke `tests/Smoke/excluded-layer-keywords`.

## Usage

Put panels or curves that the project does not track on a sublayer whose name contains
`Not in Scope` or `By Others`; the plug-in ignores them.

## Acceptance

- Managed: keyword matching variants; panel/curve predicates and curve routing exclude such
  paths; non-excluded paths unchanged; BKT layer list scope counts drop by the excluded layers.
- Debug/Release builds and the full regression pass.

## Risks and rollback

PCpid treats PID holders outside its scope as collisions, so a new PID that equals one held by an
excluded panel is refused rather than duplicated. Existing curves and surfaces of excluded panels
stay in the model. Revert the branch to restore tracking.

## Future extensions

A project-configurable keyword list, if teams need other words.
