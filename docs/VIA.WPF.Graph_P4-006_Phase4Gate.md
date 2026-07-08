# VIA.WPF.Graph – P4-006 Phase 4 Gate

## Status

Phase 4 is complete for the reusable, host-neutral VIA.WPF.Graph library surface.

This gate documents the accepted WPF hybrid UX baseline after P4-001 through P4-005. It does not add runtime code and does not introduce any UserFlow-specific dependency.

## Verified baseline

- Working solution: `VIA.WPF.Graph.slnx` in the inner solution folder.
- Build command from the inner solution folder: `dotnet test .\VIA.WPF.Graph.slnx`.
- Latest VS2AI export: `VIA.WPF.Graph.20260708-090040.vs2ai.context.md`.
- Exported source path: `C:\VIA_DEVELOPMENT\#PROJECTS\VIA.WPF.Graph\VIA.WPF.Graph`.
- Exported branch/commit: `p0-001-solution-structure`, commit `a4dd37038487a78d1dc9b107884845ea2d24ed00`.
- User confirmation before this gate: build OK and no pending Git changes.

## Completed Phase 4 scope

### P4-001 – Navigation Path Tree

Implemented `GraphNavigationPathTree` as a native WPF control for cycle-safe tree projections.

Accepted capabilities:

- Root, branch, reference, terminal and missing-target mini cards.
- Reference nodes are visually distinct.
- Click selection and double-click/open behavior produce neutral `GraphRequest` instances.
- The tree has no host-model mutation path.

### P4-002 – Tree/Graph synchronization

Implemented one-way synchronization hooks between tree and graph selection state.

Accepted capabilities:

- Tree can follow externally supplied graph node selection.
- Tree can follow externally supplied graph link selection.
- Selection synchronization does not execute host commands and does not mutate host models.
- Link selection takes precedence when both node and link selection are present.

### P4-003 – Container group collapse

Implemented container collapse/expand behavior in `GraphCanvas`.

Accepted capabilities:

- `CollapsedGroupIds` is bindable and normalized.
- `SetGroupCollapsed` and `ToggleGroupCollapsed` update visual state and emit neutral host requests.
- Collapse is restricted to layout/container groups.
- Marker groups are explicitly rejected for collapse.
- Collapsed groups render as group cards with transition-count badge.
- Internal nodes and bundled transitions are hidden/aggregated during collapse.

### P4-004 – Marker group selection, focus and filter

Implemented marker-group interaction over the neutral graph document.

Accepted capabilities:

- `GraphCanvas.Document` supplies group membership data for marker-group UX.
- Marker groups can be selected independently of container groups.
- Marker-group focus fits bounds around member nodes.
- Marker-group filter can dim or hide non-matching nodes/links.
- Marker-group filter clearing preserves non-marker group selection.
- Container groups remain the only collapse-capable groups.

### P4-005 – Area overview, focus mode and visual density

Implemented view-mode and density behavior for the standard hybrid UX.

Accepted capabilities:

- `ActiveViewMode` is bindable on `GraphCanvas`.
- `ShowAreaOverview()` switches to group/area overview and clears element focus/search.
- Area overview hides individual nodes and shows grouped area transitions.
- Focus operations set focus mode.
- `ReturnToOverview()` clears focus/search and restores overview mode.
- `VisualDensity` is constrained to the supported range.

## Phase 4 gate checklist

| Requirement | Result |
|---|---|
| Tree/path projection renders as mini-card navigation | Passed |
| Reference nodes avoid recursive tree loops | Passed |
| Tree selection can synchronize from graph selection | Passed |
| Graph selection/open operations use neutral requests | Passed |
| Container groups can collapse/expand | Passed |
| Collapse is not available for marker groups | Passed |
| Collapsed containers aggregate external transitions | Passed |
| Marker groups can be selected/focused/filtered | Passed |
| Focus and overview modes are represented in control state | Passed |
| WPF renderer remains independent from Graphviz rendering | Passed |
| No UserFlow types, names or dependencies are introduced | Passed |
| Host mutation remains outside the library | Passed |

## Explicitly not part of this gate

The following topics remain outside Phase 4 and must not be treated as implemented by this gate:

- polished demo/stress scenarios for 15/30/150 node graphs,
- accessibility review beyond current keyboard/focus basics,
- full visual design refinement in the demo host,
- host-side persistent view-state storage,
- undo/redo integration,
- edit contracts and mutation workflows,
- UserFlow adapter or UserFlow data model integration,
- manual layout persistence,
- screenshot/SVG/PDF export.

## Next phase

Proceed with Phase 5 only after this gate is committed and the user confirms the build result.

Phase 5 should focus on the demo and load/stress validation of the reusable WPF graph controls. It must still remain independent from UserFlow.
