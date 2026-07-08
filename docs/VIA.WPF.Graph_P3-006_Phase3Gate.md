# VIA.WPF.Graph – P3-006 Phase-3-Gate

## Status

Phase 3 is ready for local gate verification after the WPF test package has been added and all tests pass locally.

## Verified scope

- `GraphCanvas` exists in `VIA.WPF.Graph.Wpf`.
- The WPF project still references only `VIA.WPF.Graph.Core`.
- Rendering is based on neutral `GraphLayoutResult` data.
- Separate group, edge and node drawing layers are present.
- Zoom, pan, fit-to-graph and layout bounds are covered by WPF tests.
- Focus/search/return-to-overview behavior is covered by WPF tests.
- Neutral request objects are covered by Core tests.

## Explicit non-scope

- No Graphviz reference in `VIA.WPF.Graph.Wpf`.
- No UserFlow, Mockup, Screen, Popup or ActionArea dependency.
- No final visual design, templates or production GraphCanvas UX.
- No Path Tree implementation; that starts in Phase 4.

## Required local gate command

```powershell
dotnet test .\VIA.WPF.Graph\VIA.WPF.Graph.slnx
```

## Acceptance

P3-006 is accepted when all Core, Graphviz and WPF tests pass and the patch is committed and pushed.

After acceptance, Phase 3 is closed. Phase 4 starts only after explicit approval.
