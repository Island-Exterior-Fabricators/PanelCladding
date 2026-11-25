extern alias rhinocommon;

using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using ModelComponent = rhinocommon::Rhino.DocObjects.ModelComponent;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using ObjectEnumeratorSettings = rhinocommon::Rhino.DocObjects.ObjectEnumeratorSettings;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed class LivePanelCladdingUpdateService : ILivePanelCladdingUpdateService
{
    private readonly ILivePanelCladdingRepository _layouts;
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelCladdingSpawnPlanningService _spawnPlanning;
    private readonly PanelCladdingDependencyReconciliationService _reconciliation;

    public LivePanelCladdingUpdateService(
        ILivePanelCladdingRepository layouts,
        PanelCladdingKeyService keys,
        PanelCladdingSpawnPlanningService spawnPlanning,
        PanelCladdingDependencyReconciliationService reconciliation)
    {
        _layouts = layouts;
        _keys = keys;
        _spawnPlanning = spawnPlanning;
        _reconciliation = reconciliation;
    }

    public OperationResponse<PanelCladdingUpdateResult> Update(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds,
        PanelCladdingBakeScope scope)
    {
        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelCladdingUpdateResult>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;
        Guid[] sourcePanelIds = (panelObjectIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (sourcePanelIds.Length == 0)
        {
            return OperationResponse<PanelCladdingUpdateResult>.Fail(
                "PANEL_CLADDING_UPDATE_PANEL_SELECTION_REQUIRED");
        }

        // A problem of one panel skips that panel whole and marks it with a dot; the others still
        // bake. Only failures that are not about one panel stop and roll back the whole run.
        var issues = new List<PanelCladdingBakeIssue>();
        IReadOnlyList<PanelCladdingUpdateSource> sources = ReadPanelSources(document, sourcePanelIds, issues);
        return Apply(document, filePath, sourcePanelIds, sources, scope, issues);
    }

    private OperationResponse<PanelCladdingUpdateResult> UpdateDependencies(
        RhinoDoc document,
        string filePath,
        IReadOnlyList<Guid> sourcePanelIds,
        IReadOnlyList<Guid> candidatePanelIds,
        PanelCladdingBakeScope scope,
        List<PanelCladdingBakeIssue> issues,
        Func<Guid, bool> restoreSource,
        ref bool documentMutationOccurred)
    {
        // Read again after the CID writes, so selection and generation use the same document state.
        // Every selected panel with a PID stays an ownership candidate, so dependencies of skipped
        // panels are protected from another panel's update.
        IReadOnlyList<PanelCladdingUpdateSource> sources = ReadPanelSources(
            document, sourcePanelIds, new List<PanelCladdingBakeIssue>());
        PanelCladdingUpdateSelection selection = PanelCladdingUpdateSelectionService.CreatePlan(sources);
        var candidates = candidatePanelIds.ToHashSet();
        PanelCladdingUpdateSource[] processable = selection.ProcessableSources
            .Where(source => candidates.Contains(source.ObjectId))
            .ToArray();
        foreach (Guid lost in candidatePanelIds.Except(processable.Select(source => source.ObjectId)).ToArray())
        {
            PanelCladdingDuplicateCidGroup? group = selection.DuplicateCidGroups
                .FirstOrDefault(item => item.PanelObjectIds.Contains(lost));
            issues.Add(group is null
                ? new PanelCladdingBakeIssue(lost, PanelCladdingBakeIssueService.MissingPanelStage,
                    $"PANEL_CLADDING_UPDATE_PANEL_BREP_NOT_FOUND: {lost:D}")
                : new PanelCladdingBakeIssue(lost, PanelCladdingBakeIssueService.DuplicateCidStage, group.Cid));
            RestoreOrThrow(restoreSource, lost);
        }

        var generator = new LivePanelCladdingSpawnService(_layouts, _keys, _spawnPlanning);
        var prepared = new List<PreparedDependency>();
        try
        {
            var baked = new List<PanelCladdingUpdateSource>();
            foreach (PanelCladdingUpdateSource source in processable)
            {
                PanelCladdingBakeIssue? issue = PreparePanelDependencies(
                    document, filePath, source, generator, scope, prepared);
                if (issue is not null)
                {
                    issues.Add(issue);
                    RestoreOrThrow(restoreSource, source.ObjectId);
                    continue;
                }
                baked.Add(source);
            }

            var ambiguousDependencies = new List<Guid>();
            IReadOnlyList<PanelCladdingExistingDependency> existing = ReadManagedDependencies(
                document,
                sources,
                baked.Select(source => source.ObjectId).ToHashSet(),
                ambiguousDependencies);
            OperationResponse<PanelCladdingDependencyReconciliationPlan> plan =
                _reconciliation.CreatePlan(
                    prepared.Select(item => item.Expected).ToArray(),
                    existing,
                    scope);
            if (!plan.Success || plan.Data is null)
            {
                return OperationResponse<PanelCladdingUpdateResult>.Fail(plan.Message);
            }

            return ApplyDependencies(document, baked, selection.DuplicateCidGroups, prepared, plan.Data,
                ambiguousDependencies, ref documentMutationOccurred);
        }
        finally
        {
            DisposePrepared(prepared);
        }
    }

    private static void RestoreOrThrow(Func<Guid, bool> restoreSource, Guid panel)
    {
        if (!restoreSource(panel))
        {
            throw new InvalidOperationException($"SOURCE_ROLLBACK_FAILED: {panel:D}");
        }
    }

    // Selected panels that carry a PID, for ownership. Missing panels, panels without a PID and
    // panels that must not be edited are reported as issues.
    private static IReadOnlyList<PanelCladdingUpdateSource> ReadPanelSources(
        RhinoDoc document,
        IReadOnlyList<Guid> panelObjectIds,
        List<PanelCladdingBakeIssue> issues)
    {
        var sources = new List<PanelCladdingUpdateSource>(panelObjectIds.Count);
        foreach (Guid objectId in panelObjectIds)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject?.Geometry is not Brep)
            {
                issues.Add(new PanelCladdingBakeIssue(objectId, PanelCladdingBakeIssueService.MissingPanelStage,
                    $"PANEL_CLADDING_UPDATE_PANEL_BREP_NOT_FOUND: {objectId:D}"));
                continue;
            }
            string panelId = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey).Trim();
            if (panelId.Length == 0)
            {
                issues.Add(new PanelCladdingBakeIssue(objectId, PanelCladdingBakeIssueService.PidStage,
                    $"PANEL_CLADDING_UPDATE_PID_REQUIRED: {objectId:D}"));
                continue;
            }
            if (rhinoObject.IsReference || rhinoObject.IsLocked || rhinoObject.IsHidden)
            {
                issues.Add(new PanelCladdingBakeIssue(objectId, PanelCladdingBakeIssueService.NotEditableStage,
                    $"PANEL_CLADDING_UPDATE_SOURCE_NOT_EDITABLE: {objectId:D}"));
            }
            sources.Add(new PanelCladdingUpdateSource(objectId, panelId,
                PanelCladdingCidService.ResolvePanelCid(panelId, LivePanelCladdingCidService.Read(rhinoObject.Attributes))));
        }
        return sources;
    }

    // Generates one panel's surfaces and curves into `prepared`; on any problem nothing of the
    // panel is kept and the issue is returned.
    private static PanelCladdingBakeIssue? PreparePanelDependencies(
        RhinoDoc document,
        string filePath,
        PanelCladdingUpdateSource source,
        LivePanelCladdingSpawnService generator,
        PanelCladdingBakeScope scope,
        List<PreparedDependency> prepared)
    {
        var panel = new List<PreparedDependency>();
        PanelCladdingBakeIssue? issue = null;
        try
        {
            if (PanelCladdingDependencyReconciliationService.Includes(scope, PanelCladdingDependencyKind.Surface))
            {
                OperationResponse<LivePanelCladdingSpawnService.PreparedPanel> surfaces =
                    generator.PreparePanel(
                        document,
                        filePath,
                        source.ObjectId,
                        PanelCladdingObjectScope.Surfaces,
                        allowEmptySurfacePlan: true);
                if (!surfaces.Success || surfaces.Data is null)
                {
                    return issue = new PanelCladdingBakeIssue(
                        source.ObjectId, PanelCladdingBakeIssueService.SurfaceStage, surfaces.Message);
                }
                panel.AddRange(surfaces.Data.Regions.Select(region =>
                    PreparedDependency.FromSurface(source, region)));
            }

            if (PanelCladdingDependencyReconciliationService.Includes(scope, PanelCladdingDependencyKind.Curve))
            {
                OperationResponse<LivePanelCladdingSpawnService.PreparedPanel> curves =
                    generator.PreparePanel(
                        document,
                        filePath,
                        source.ObjectId,
                        PanelCladdingObjectScope.Curves);
                if (!curves.Success || curves.Data is null)
                {
                    return issue = new PanelCladdingBakeIssue(
                        source.ObjectId, PanelCladdingBakeIssueService.CurveStage, curves.Message);
                }
                panel.AddRange(curves.Data.Curves.Select(curve =>
                    PreparedDependency.FromCurve(source, curve)));
            }

            // Checked here, before any write: a layer Rhino cannot create or two objects claiming
            // one CID would otherwise fail the whole run halfway.
            foreach (PreparedDependency dependency in panel)
            {
                string stage = dependency.Expected.Kind == PanelCladdingDependencyKind.Surface
                    ? PanelCladdingBakeIssueService.SurfaceStage
                    : PanelCladdingBakeIssueService.CurveStage;
                string layerPath = dependency.SurfacePlan?.LayerPath ?? dependency.CurvePlan?.LayerPath ?? string.Empty;
                if (!IsCreatableLayerPath(document, layerPath))
                {
                    return issue = new PanelCladdingBakeIssue(source.ObjectId, stage,
                        $"PANEL_CLADDING_LAYER_NAME_INVALID: {layerPath}");
                }
            }
            PreparedDependency? duplicate = panel
                .GroupBy(dependency => IdentityKey(dependency.Expected), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.First())
                .FirstOrDefault();
            if (duplicate is not null)
            {
                return issue = new PanelCladdingBakeIssue(source.ObjectId,
                    duplicate.Expected.Kind == PanelCladdingDependencyKind.Surface
                        ? PanelCladdingBakeIssueService.SurfaceStage
                        : PanelCladdingBakeIssueService.CurveStage,
                    $"PANEL_CLADDING_UPDATE_DUPLICATE_EXPECTED_CID: {duplicate.Expected.Cid}");
            }
            return null;
        }
        catch (Exception ex)
        {
            return issue = new PanelCladdingBakeIssue(source.ObjectId,
                PanelCladdingDependencyReconciliationService.Includes(scope, PanelCladdingDependencyKind.Surface)
                    ? PanelCladdingBakeIssueService.SurfaceStage
                    : PanelCladdingBakeIssueService.CurveStage,
                $"PANEL_CLADDING_UPDATE_PREPARE_FAILED: {ex.Message}");
        }
        finally
        {
            if (issue is null)
            {
                prepared.AddRange(panel);
            }
            else
            {
                DisposePrepared(panel);
            }
        }
    }

    private static bool IsCreatableLayerPath(RhinoDoc document, string layerPath) =>
        layerPath.Length > 0 &&
        (document.Layers.FindByFullPath(layerPath, -1) >= 0 ||
            layerPath.Split("::").All(segment => segment.Length > 0 && ModelComponent.IsValidComponentName(segment)));

    private static IReadOnlyList<PanelCladdingExistingDependency> ReadManagedDependencies(
        RhinoDoc document,
        IReadOnlyList<PanelCladdingUpdateSource> sources,
        IReadOnlySet<Guid> processableIds,
        List<Guid> ambiguousDependencies)
    {
        var selectedPanels = sources.GroupBy(source => source.PanelId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var sourceIds = sources.Select(source => source.ObjectId).ToHashSet();
        var result = new List<PanelCladdingExistingDependency>();
        foreach (RhinoObject rhinoObject in document.Objects.GetObjectList(
            new ObjectEnumeratorSettings
            {
                NormalObjects = true,
                LockedObjects = true,
                HiddenObjects = true,
                ActiveObjects = true,
                ReferenceObjects = false,
                ObjectTypeFilter = ObjectType.Brep | ObjectType.Curve
            }))
        {
            if (sourceIds.Contains(rhinoObject.Id))
            {
                continue;
            }
            string layerPath = rhinoObject.Attributes.LayerIndex >= 0
                ? document.Layers[rhinoObject.Attributes.LayerIndex]?.FullPath ?? string.Empty
                : string.Empty;
            PanelCladdingDependencyKind? kind = rhinoObject.Geometry switch
            {
                Brep when PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(layerPath) =>
                    PanelCladdingDependencyKind.Surface,
                Curve when PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(layerPath,
                        PanelCladdingProjectService.CodeFromDocumentPath(document.Path)) =>
                    PanelCladdingDependencyKind.Curve,
                _ => null
            };
            if (!kind.HasValue)
            {
                continue;
            }

            string panelId = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey).Trim();
            string cid = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.CidUserTextKey).Trim();
            if (!selectedPanels.TryGetValue(panelId, out var candidates) || cid.Length == 0)
            {
                continue;
            }
            var owners = PanelCladdingUpdateSelectionService.FindDependencyOwners(candidates, panelId, cid);
            if (owners.Count != 1 || !processableIds.Contains(owners[0].ObjectId))
            {
                // Legacy unsuffixed dependencies cannot be assigned to one of several selected roles.
                // Including skipped sources here also protects their geometry from another panel's update.
                if (owners.Count > 1 && owners.Any(owner => processableIds.Contains(owner.ObjectId)))
                {
                    ambiguousDependencies.Add(rhinoObject.Id);
                }
                continue;
            }
            result.Add(new PanelCladdingExistingDependency
            {
                ObjectId = rhinoObject.Id,
                SourcePanelObjectId = owners[0].ObjectId,
                PanelId = panelId,
                Cid = cid,
                Kind = kind.Value
            });
        }
        return result;
    }

    private OperationResponse<PanelCladdingUpdateResult> Apply(
        RhinoDoc document,
        string filePath,
        IReadOnlyList<Guid> sourcePanelIds,
        IReadOnlyList<PanelCladdingUpdateSource> sources,
        PanelCladdingBakeScope scope,
        List<PanelCladdingBakeIssue> issues)
    {
        var selection = PanelCladdingUpdateSelectionService.CreatePlan(sources);
        foreach (PanelCladdingDuplicateCidGroup group in selection.DuplicateCidGroups)
        {
            issues.AddRange(group.PanelObjectIds.Select(id =>
                new PanelCladdingBakeIssue(id, PanelCladdingBakeIssueService.DuplicateCidStage, group.Cid)));
        }
        var pointOrder = new Dictionary<Guid, LivePanelCladdingPointOrderService.PreparedGeometry>();
        try
        {
            var candidates = new List<Guid>();
            foreach (var source in selection.ProcessableSources)
            {
                if (issues.Any(issue => issue.PanelObjectId == source.ObjectId)) continue;
                var panel = document.Objects.FindId(source.ObjectId)!;
                OperationResponse<LivePanelCladdingPointOrderService.PreparedGeometry> prepared;
                try
                {
                    prepared = new LivePanelCladdingPointOrderService().Prepare((Brep)panel.Geometry, document.ModelAbsoluteTolerance);
                }
                catch (Exception exception)
                {
                    prepared = OperationResponse<LivePanelCladdingPointOrderService.PreparedGeometry>.Fail(
                        $"PANEL_CLADDING_UPDATE_POINT_ORDER_FAILED: {exception.Message}");
                }
                if (!prepared.Success || prepared.Data is null)
                {
                    issues.Add(new PanelCladdingBakeIssue(
                        source.ObjectId, PanelCladdingBakeIssueService.PointOrderStage, prepared.Message));
                    continue;
                }
                pointOrder.Add(source.ObjectId, prepared.Data);
                candidates.Add(source.ObjectId);
            }
            return ApplyPrepared(document, filePath, sourcePanelIds, candidates, scope, pointOrder, issues);
        }
        catch (Exception exception)
        {
            return OperationResponse<PanelCladdingUpdateResult>.Fail($"PANEL_CLADDING_UPDATE_FAILED: {exception.Message}");
        }
        finally
        {
            foreach (var prepared in pointOrder.Values) prepared.Dispose();
        }
    }

    // Skipped panels are not changed at all: only the candidates get CID and point-order writes,
    // and a candidate that fails later has both restored before the run goes on.
    private OperationResponse<PanelCladdingUpdateResult> ApplyPrepared(
        RhinoDoc document,
        string filePath,
        IReadOnlyList<Guid> sourcePanelIds,
        IReadOnlyList<Guid> candidatePanelIds,
        PanelCladdingBakeScope scope,
        IReadOnlyDictionary<Guid, LivePanelCladdingPointOrderService.PreparedGeometry> pointOrder,
        List<PanelCladdingBakeIssue> issues)
    {
        var panelCidChanges = LivePanelCladdingCidService.Prepare(document, candidatePanelIds);
        if (!panelCidChanges.Success || panelCidChanges.Data is null)
        {
            return OperationResponse<PanelCladdingUpdateResult>.Fail(panelCidChanges.Message);
        }
        uint undoRecord = document.CurrentUndoRecordSerialNumber;
        bool ownsUndoRecord = undoRecord == 0U;
        if (ownsUndoRecord)
        {
            undoRecord = document.BeginUndoRecord("Update Panel Cladding Dependencies");
        }
        if (undoRecord == 0U)
        {
            foreach (var change in panelCidChanges.Data) { change.Original.Dispose(); change.Proposed.Dispose(); }
            return OperationResponse<PanelCladdingUpdateResult>.Fail(
                "PANEL_CLADDING_UPDATE_UNDO_UNAVAILABLE");
        }

        bool documentMutationOccurred = false;
        var restoredPanels = new HashSet<Guid>();
        bool RestoreSource(Guid panel)
        {
            bool restored = !pointOrder.TryGetValue(panel, out var geometry) || geometry.Restore(document, panel);
            restored &= LivePanelCladdingCidService.Restore(
                document, panelCidChanges.Data.Where(change => change.ObjectId == panel).ToArray());
            restoredPanels.Add(panel);
            return restored;
        }

        OperationResponse<PanelCladdingUpdateResult>? outcome = null;
        try
        {
            OperationResponse cidCommit = LivePanelCladdingCidService.Apply(document, panelCidChanges.Data);
            documentMutationOccurred |= panelCidChanges.Data.Any(change => change.Applied);
            if (cidCommit.Success)
            {
                foreach (var (id, geometry) in pointOrder)
                {
                    bool applied = geometry.Apply(document, id);
                    documentMutationOccurred |= geometry.Applied;
                    if (!applied) throw new InvalidOperationException($"Point-order write failed for panel {id}.");
                }
            }
            outcome = cidCommit.Success
                ? UpdateDependencies(document, filePath, sourcePanelIds, candidatePanelIds, scope, issues,
                    RestoreSource, ref documentMutationOccurred)
                : OperationResponse<PanelCladdingUpdateResult>.Fail(cidCommit.Message);
            if (outcome.Success && outcome.Data is not null)
            {
                PanelCladdingUpdateResult result = outcome.Data;
                result.ReorderedPanelIds = pointOrder
                    .Where(p => p.Value.Changed && !restoredPanels.Contains(p.Key)).Select(p => p.Key).ToArray();
                result.PointOrderWarnings = pointOrder
                    .Where(p => p.Value.SkipReason.Length > 0 && !restoredPanels.Contains(p.Key))
                    .Select(p => $"Panel {p.Key}: {p.Value.SkipReason}").ToArray();
                result.Issues = PanelCladdingBakeIssueService.Merge(issues);
                OperationResponse marked = LivePanelCladdingBakeIssueDots.Mark(
                    document, sourcePanelIds, result.Issues, ref documentMutationOccurred);
                if (!marked.Success)
                {
                    outcome = OperationResponse<PanelCladdingUpdateResult>.Fail(marked.Message);
                }
                else
                {
                    if (result.Issues.Count > 0)
                    {
                        // Skipped panels stay selected for inspection.
                        document.Objects.UnselectAll();
                        foreach (Guid id in result.Issues.Select(issue => issue.PanelObjectId))
                        {
                            document.Objects.FindId(id)?.Select(true);
                        }
                    }
                    document.Views.Redraw();
                }
            }
        }
        catch (Exception ex)
        {
            outcome = OperationResponse<PanelCladdingUpdateResult>.Fail(
                $"PANEL_CLADDING_UPDATE_FAILED: {ex.Message}");
        }
        finally
        {
            // An ambient Rhino command owns its Undo record. Restore our source
            // geometry/CID changes explicitly without ending or undoing that record.
            if (!ownsUndoRecord && outcome is { Success: false })
            {
                bool restored = true;
                foreach (var (id, geometry) in pointOrder.Reverse().Where(p => !restoredPanels.Contains(p.Key)))
                {
                    try { restored &= geometry.Restore(document, id); }
                    catch { restored = false; }
                }
                try
                {
                    restored &= LivePanelCladdingCidService.Restore(document,
                        panelCidChanges.Data.Where(change => !restoredPanels.Contains(change.ObjectId)).ToArray());
                }
                catch { restored = false; }
                if (!restored) outcome = OperationResponse<PanelCladdingUpdateResult>.Fail(outcome.Message + "; SOURCE_ROLLBACK_FAILED");
            }
            if (ownsUndoRecord)
            {
                document.EndUndoRecord(undoRecord);
                if (outcome is { Success: false } && documentMutationOccurred && !document.Undo())
                {
                    outcome = OperationResponse<PanelCladdingUpdateResult>.Fail(
                        $"{outcome.Message}; PANEL_CLADDING_UPDATE_ROLLBACK_FAILED");
                }
            }
            foreach (var change in panelCidChanges.Data) { change.Original.Dispose(); change.Proposed.Dispose(); }
        }
        return outcome ?? OperationResponse<PanelCladdingUpdateResult>.Fail(
            "PANEL_CLADDING_UPDATE_NO_RESULT");
    }

    private static OperationResponse<PanelCladdingUpdateResult> ApplyDependencies(
        RhinoDoc document,
        IReadOnlyList<PanelCladdingUpdateSource> bakedSources,
        IReadOnlyList<PanelCladdingDuplicateCidGroup> duplicateCidGroups,
        IReadOnlyList<PreparedDependency> prepared,
        PanelCladdingDependencyReconciliationPlan plan,
        IReadOnlyList<Guid> ambiguousDependencies,
        ref bool documentMutationOccurred)
    {
        var preparedByIdentity = prepared.ToDictionary(
            item => IdentityKey(item.Expected), StringComparer.OrdinalIgnoreCase);
        var createdSurfaces = new List<Guid>();
        var createdCurves = new List<Guid>();
        var updatedSurfaces = new List<Guid>();
        var updatedCurves = new List<Guid>();
        var deleted = new List<Guid>();
        foreach (PanelCladdingDependencyUpdateAction action in plan.Updates)
        {
            PreparedDependency dependency = preparedByIdentity[IdentityKey(action.Expected)];
            OperationResponse updated = UpdateExisting(
                document, action.ObjectId, dependency, ref documentMutationOccurred);
            if (!updated.Success)
            {
                return OperationResponse<PanelCladdingUpdateResult>.Fail(updated.Message);
            }
            (dependency.Expected.Kind == PanelCladdingDependencyKind.Surface
                ? updatedSurfaces : updatedCurves).Add(action.ObjectId);
        }
        foreach (PanelCladdingExpectedDependency expected in plan.Creates)
        {
            PreparedDependency dependency = preparedByIdentity[IdentityKey(expected)];
            OperationResponse<Guid> created = CreateMissing(document, dependency, ref documentMutationOccurred);
            if (!created.Success || created.Data == Guid.Empty)
            {
                return OperationResponse<PanelCladdingUpdateResult>.Fail(created.Message);
            }
            (expected.Kind == PanelCladdingDependencyKind.Surface
                ? createdSurfaces : createdCurves).Add(created.Data);
            documentMutationOccurred = true;
        }
        foreach (Guid objectId in plan.Deletes)
        {
            RhinoObject? existingObject = document.Objects.FindId(objectId);
            if (existingObject is null || !document.Objects.Delete(
                    existingObject,
                    quiet: true,
                    ignoreModes: true))
            {
                return OperationResponse<PanelCladdingUpdateResult>.Fail(
                    $"PANEL_CLADDING_UPDATE_DELETE_FAILED: {objectId:D}");
            }
            deleted.Add(objectId);
            documentMutationOccurred = true;
        }
        return OperationResponse<PanelCladdingUpdateResult>.Ok(new PanelCladdingUpdateResult
        {
            SourcePanelIds = bakedSources.Select(source => source.ObjectId).ToArray(),
            DuplicateCidGroups = duplicateCidGroups,
            PreservedAmbiguousDependencyIds = ambiguousDependencies,
            CreatedSurfaceIds = createdSurfaces,
            CreatedCurveIds = createdCurves,
            UpdatedSurfaceIds = updatedSurfaces,
            UpdatedCurveIds = updatedCurves,
            DeletedObjectIds = deleted
        });
    }

    private static OperationResponse UpdateExisting(
        RhinoDoc document,
        Guid objectId,
        PreparedDependency dependency,
        ref bool documentMutationOccurred)
    {
        RhinoObject? existingObject = document.Objects.FindId(objectId);
        if (existingObject is null)
        {
            return OperationResponse.Fail($"PANEL_CLADDING_UPDATE_REPLACE_FAILED: {objectId:D}");
        }

        OperationResponse<ObjectAttributes> attributes = CreateAttributes(
            document,
            dependency,
            ref documentMutationOccurred);
        if (!attributes.Success || attributes.Data is null)
        {
            return OperationResponse.Fail(attributes.Message);
        }

        attributes.Data.Mode = existingObject.Attributes.Mode;
        bool replaced = document.Objects.Replace(
            objectId,
            dependency.Geometry,
            ignoreModes: true);
        if (!replaced)
        {
            return OperationResponse.Fail($"PANEL_CLADDING_UPDATE_REPLACE_FAILED: {objectId:D}");
        }
        documentMutationOccurred = true;
        return document.Objects.ModifyAttributes(objectId, attributes.Data, quiet: true)
            ? OperationResponse.Ok()
            : OperationResponse.Fail($"PANEL_CLADDING_UPDATE_ATTRIBUTES_FAILED: {objectId:D}");
    }

    private static OperationResponse<Guid> CreateMissing(
        RhinoDoc document,
        PreparedDependency dependency,
        ref bool documentMutationOccurred)
    {
        OperationResponse<ObjectAttributes> attributes = CreateAttributes(
            document,
            dependency,
            ref documentMutationOccurred);
        if (!attributes.Success || attributes.Data is null)
        {
            return OperationResponse<Guid>.Fail(attributes.Message);
        }
        Guid objectId = dependency.Geometry switch
        {
            Brep brep => document.Objects.AddBrep(brep, attributes.Data),
            Curve curve => document.Objects.AddCurve(curve, attributes.Data),
            _ => Guid.Empty
        };
        return objectId == Guid.Empty
            ? OperationResponse<Guid>.Fail(
                $"PANEL_CLADDING_UPDATE_CREATE_FAILED: {dependency.Expected.Cid}")
            : OperationResponse<Guid>.Ok(objectId);
    }

    private static OperationResponse<ObjectAttributes> CreateAttributes(
        RhinoDoc document,
        PreparedDependency dependency,
        ref bool documentMutationOccurred)
    {
        string layerPath = dependency.SurfacePlan?.LayerPath ??
            dependency.CurvePlan?.LayerPath ??
            string.Empty;
        int priorLayerIndex = document.Layers.FindByFullPath(layerPath, -1);
        bool layerNeedsMutation = priorLayerIndex < 0;
        if (!layerNeedsMutation && dependency.SurfacePlan is not null)
        {
            var expectedColor = System.Drawing.Color.FromArgb(
                dependency.SurfacePlan.LayerColor.Red,
                dependency.SurfacePlan.LayerColor.Green,
                dependency.SurfacePlan.LayerColor.Blue);
            layerNeedsMutation = document.Layers[priorLayerIndex]?.Color.ToArgb() != expectedColor.ToArgb();
        }

        OperationResponse<int> layer = dependency.SurfacePlan is not null
            ? LivePanelCladdingSpawnService.EnsureMaterialLayer(document, dependency.SurfacePlan)
            : LivePanelCladdingSpawnService.EnsureLayer(
                document,
                layerPath);
        if (layerNeedsMutation && document.Layers.FindByFullPath(layerPath, -1) >= 0)
        {
            documentMutationOccurred = true;
        }
        if (!layer.Success)
        {
            return OperationResponse<ObjectAttributes>.Fail(layer.Message);
        }

        var attributes = new ObjectAttributes
        {
            Name = PanelCladdingCidService.ShortName(dependency.Expected.Cid),
            LayerIndex = layer.Data,
            ColorSource = dependency.CurvePlan is null
                ? ObjectColorSource.ColorFromLayer
                : ObjectColorSource.ColorFromObject
        };
        if (dependency.CurvePlan is not null)
        {
            attributes.ObjectColor = System.Drawing.Color.FromArgb(
                dependency.CurvePlan.ObjectColor.Red,
                dependency.CurvePlan.ObjectColor.Green,
                dependency.CurvePlan.ObjectColor.Blue);
        }
        IReadOnlyDictionary<string, string> writes = dependency.SurfacePlan?.UserTextWrites ??
            dependency.CurvePlan?.UserTextWrites ??
            new Dictionary<string, string>();
        foreach ((string key, string value) in writes)
        {
            attributes.SetUserString(key, value);
        }
        return OperationResponse<ObjectAttributes>.Ok(attributes);
    }

    private static OperationResponse<RhinoDoc> ResolveDocument(string filePath)
    {
        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null)
        {
            return OperationResponse<RhinoDoc>.Fail("NO_ACTIVE_DOCUMENT");
        }
        if (string.IsNullOrWhiteSpace(document.Path))
        {
            return OperationResponse<RhinoDoc>.Fail("ACTIVE_DOC_UNSAVED");
        }
        try
        {
            if (!string.Equals(
                Path.GetFullPath(document.Path).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<RhinoDoc>.Fail("FILE_NOT_ACTIVE");
            }
        }
        catch (Exception ex)
        {
            return OperationResponse<RhinoDoc>.Fail($"DOCUMENT_PATH_INVALID: {ex.Message}");
        }
        return OperationResponse<RhinoDoc>.Ok(document);
    }

    private static string GetCanonicalUserText(ObjectAttributes attributes, string canonicalKey)
    {
        var values = attributes.GetUserStrings();
        foreach (string? key in values?.AllKeys ?? Array.Empty<string?>())
        {
            if (key is not null && string.Equals(key, canonicalKey, StringComparison.OrdinalIgnoreCase))
            {
                return values?[key] ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static string IdentityKey(PanelCladdingExpectedDependency item) =>
        $"{item.Kind}|{item.PanelId.Trim()}|{item.Cid.Trim()}";

    private static void DisposePrepared(IEnumerable<PreparedDependency> dependencies)
    {
        foreach (PreparedDependency dependency in dependencies)
        {
            dependency.Dispose();
        }
    }

    private sealed class PreparedDependency : IDisposable
    {
        private PreparedDependency(
            PanelCladdingExpectedDependency expected,
            GeometryBase geometry,
            PanelCladdingSpawnRegionPlan? surfacePlan,
            PanelCladdingExtrusionCurvePlan? curvePlan)
        {
            Expected = expected;
            Geometry = geometry;
            SurfacePlan = surfacePlan;
            CurvePlan = curvePlan;
        }

        public PanelCladdingExpectedDependency Expected { get; }
        public GeometryBase Geometry { get; }
        public PanelCladdingSpawnRegionPlan? SurfacePlan { get; }
        public PanelCladdingExtrusionCurvePlan? CurvePlan { get; }

        public static PreparedDependency FromSurface(
            PanelCladdingUpdateSource source,
            LivePanelCladdingSpawnService.PreparedRegion region) =>
            new(
                new PanelCladdingExpectedDependency
                {
                    SourcePanelObjectId = source.ObjectId,
                    PanelId = source.PanelId,
                    Cid = region.Region.Cid,
                    Kind = PanelCladdingDependencyKind.Surface
                },
                region.Geometry,
                region.Region,
                null);

        public static PreparedDependency FromCurve(
            PanelCladdingUpdateSource source,
            LivePanelCladdingSpawnService.PreparedCurve curve) =>
            new(
                new PanelCladdingExpectedDependency
                {
                    SourcePanelObjectId = source.ObjectId,
                    PanelId = source.PanelId,
                    Cid = curve.CurvePlan.Cid,
                    Kind = PanelCladdingDependencyKind.Curve
                },
                curve.Geometry,
                null,
                curve.CurvePlan);

        public void Dispose() => Geometry.Dispose();
    }
}
