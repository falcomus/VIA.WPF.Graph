# VIA.WPF.Graph – P1-006 Phase-1-Gate

## Status

Phase 1 is closed by this step after the Core test project builds and all tests pass locally.

## Scope

P1-006 adds a dedicated xUnit test project for the neutral Core library:

- `VIA.WPF.Graph.Core.Tests`
- reference only to `VIA.WPF.Graph.Core`
- no WPF dependency
- no Graphviz dependency
- no Demo dependency
- no UserFlow or Mockup dependency

## Verified Core areas

The test package covers the Phase-1 Core scope:

- neutral graph document, node, link, group and size model
- container groups versus marker groups
- host-owned selection and view-state data
- graph validation result and issue codes
- duplicate node/link/group ids
- missing link targets and missing groups
- self-links and parallel links as diagnostic cases
- invalid group hierarchy and parent cycles
- cycle-safe tree projection
- reference nodes for cycles and back links
- terminal nodes for external targets
- missing-target tree nodes
- multiple roots and unreachable components

## Test framework decision

xUnit is used for the Core tests.

Package versions used:

- `xunit` 2.9.3
- `xunit.runner.visualstudio` 3.1.5
- `Microsoft.NET.Test.Sdk` 18.6.0

## Phase-1 result

After successful local `dotnet test` or Visual Studio Test Explorer execution, Phase 1 is considered technically ready for the Step-Gate.

Phase 2 must not start automatically. It starts only after explicit user approval.

## Open items after Phase 1

The following topics are intentionally not part of Phase 1:

- Graphviz layout adapter
- DOT generation
- WPF GraphCanvas
- WPF TreeView
- Demo integration of the neutral Core model
- host mutation requests
- UserFlow integration
- persistence and migration
