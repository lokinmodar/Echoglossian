# Local Test Validation and DalaMock Complement Design

## Status

Approved in discussion on 2026-07-16. Written spec pending final user review
before implementation planning.

## Goal

Keep `Echoglossian.sln` unchanged, stop using GitHub plugin-build workflows,
standardize local test validation with one explicit script, and add DalaMock
as a complementary local testability layer for startup and service wiring
without replatforming the plugin architecture.

## Constraints

- Keep `Echoglossian.sln` unchanged.
- Do not add `Echoglossian.Tests.csproj` to the solution.
- Do not introduce a GitHub workflow that builds or tests the plugin.
- Keep `pages.yml` as the only active GitHub workflow in scope for this docs
  initiative.
- Disable active GitHub workflows that build or deploy the plugin from this
  repository.
- Keep the authoritative deploy flow via D17, outside these workflows.
- Keep plugin build and test validation local-only.
- Add DalaMock as an adendo, not a replacement for `Echoglossian.Tests`.
- Do not migrate the existing test suite in bulk.
- Do not convert the plugin to `HostedPlugin`, Autofac, or a new primary host
  architecture.
- Preserve the current `Echoglossian : IDalamudPlugin` entrypoint.
- Keep the first DalaMock cut narrowly focused on startup, lifecycle hooks,
  dependency resolution seams, and wiring that is awkward to validate with
  unit tests alone.

## Selected Approach

Use a balanced approach:

- keep `Echoglossian.Tests` as the existing unit-test rail
- add one local validation script that runs the explicit restore/build/test
  sequence the repository now requires because tests are outside the solution
- add DalaMock as a second, complementary local-only rail for runtime-adjacent
  validation
- disable GitHub plugin-build workflows instead of replacing them with a new
  CI pipeline

This approach was selected over:

- a minimal fix that only adds an explicit test script, because it would not
  improve testability for runtime-coupled plugin startup behavior
- a broad DalaMock migration, because that would expand into architecture
  rewrite and test-suite churn far beyond the requested adendo

## Current Problem

`Echoglossian.sln` currently contains only `Echoglossian.csproj`. As a result:

- `dotnet build .\Echoglossian.sln -c Debug --no-restore` builds the plugin but
  does not produce `Echoglossian.Tests.dll`
- `dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
  fails in a fresh worktree unless `Echoglossian.Tests.csproj` was explicitly
  built first

That behavior is acceptable as long as the repository owns it explicitly
instead of pretending solution-wide build coverage exists.

The repository also contains GitHub workflows that build the plugin. Those
workflows no longer match the desired operating model, where plugin validation
is local and deployment is handled through D17 rather than GitHub Actions.

## Repository Layout

The intended structure after implementation is:

```text
Echoglossian/
|-- Echoglossian.csproj
|-- Echoglossian.sln
|-- Echoglossian.Tests/
|-- Echoglossian.Mock/
|   |-- Echoglossian.Mock.csproj
|   `-- Program.cs
|-- Echoglossian.Mock.Tests/
|   `-- Echoglossian.Mock.Tests.csproj
|-- scripts/
|   `-- validate-local-tests.ps1
`-- .github/
    `-- workflows/
        `-- pages.yml
```

The plugin project remains at the repository root. The mock runner and mock
tests remain outside the solution, just like `Echoglossian.Tests`.

## GitHub Workflow Policy

The repository will keep GitHub automation only where it still adds value
without building the plugin.

### Keep

- `.github/workflows/pages.yml`

This workflow remains active because it builds only the docs site and is
independent from the plugin deploy path.

### Disable

- `.github/workflows/build.yml`
- `.github/workflows/dotnet.yml`

These workflows are related to plugin build and/or plugin test execution in
GitHub Actions. They should be removed from active use so the repository stops
advertising GitHub-based plugin validation or deployment as a supported flow.

The recommended implementation is to delete them rather than archive them in
place. Git history already preserves the old behavior, and removing the active
workflow files avoids future ambiguity.

## Local Validation Strategy

Local validation becomes explicit and script-driven instead of relying on the
solution layout.

### Canonical Script

Add one script:

- `scripts/validate-local-tests.ps1`

This script becomes the canonical local validation entrypoint for the plugin
test rails. It should be deterministic, small, and explicit rather than
feature-rich.

### Validation Sequence

The script will run these commands in order:

```powershell
dotnet restore .\Echoglossian.Tests\Echoglossian.Tests.csproj
dotnet build .\Echoglossian.sln -c Debug --no-restore
dotnet build .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
dotnet restore .\Echoglossian.Mock\Echoglossian.Mock.csproj
dotnet build .\Echoglossian.Mock\Echoglossian.Mock.csproj -c Debug --no-restore
dotnet restore .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj
dotnet build .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build
```

The purpose of this script is not to hide what happens. Its purpose is to make
the local sequence repeatable and unambiguous.

## DalaMock Integration Boundary

DalaMock is used here as a host/runtime complement, not as a replacement test
framework and not as the new plugin architecture.

Per the DalaMock project, its role is to run a Dalamud plugin outside FFXIV in
a standalone host with mock or real Dalamud service implementations, supporting
UI iteration, service wiring, and tests without the game running. Source:
[Critical-Impact/DalaMock](https://github.com/Critical-Impact/DalaMock)

### What DalaMock Will Cover

The first DalaMock rail will cover:

- plugin startup without the game
- basic dependency-resolution seams around startup
- registration and cleanup of lifecycle/UI hooks
- runtime/service wiring that is difficult to assert through isolated unit
  tests alone

### What DalaMock Will Not Cover

The first DalaMock rail will not attempt to cover:

- the entire existing `Echoglossian.Tests` suite
- native UI handlers broadly
- all translation surfaces
- a full end-to-end translation runtime
- a rewrite of the plugin's DI model
- a migration to `DalaMock.Host` as the primary plugin entrypoint pattern

## DalaMock Project Design

### `Echoglossian.Mock`

`Echoglossian.Mock` is a local executable runner. Its job is to boot the
plugin under DalaMock outside the game so startup and wiring can be exercised
manually.

This project should:

- reference the real plugin project
- reference the minimum DalaMock packages needed to host the plugin outside
  the game
- avoid introducing alternate business logic paths
- reuse the real plugin startup path through a narrow bootstrap seam rather
  than duplicating the plugin constructor logic

The initial integration should prefer the lighter DalaMock runtime path over a
full conversion to the `DalaMock.Host` hosted-plugin abstraction.

### `Echoglossian.Mock.Tests`

`Echoglossian.Mock.Tests` is a small complementary test project. It exists to
assert the first runtime-adjacent behaviors that are painful to validate in the
current unit-test rail.

The first required scenarios are:

- the plugin can be instantiated under the DalaMock-backed host
- the startup seam resolves required services for initial composition
- startup registers the expected top-level lifecycle/UI hooks
- disposal/unload removes or tears down those hooks cleanly

The first implementation should keep this suite intentionally narrow. It is a
smoke-and-wiring rail, not a second copy of `Echoglossian.Tests`.

## Plugin Composition Strategy

The current plugin entrypoint is a root-level partial
`Echoglossian : IDalamudPlugin` with direct use of static plugin services and
`PluginInterface`.

That existing model stays in place.

The correct integration point is therefore a narrow bootstrap seam extracted
from startup, not a new primary host abstraction. The implementation should:

- isolate the smallest startup/composition logic that can be exercised both
  in the real plugin path and in DalaMock
- keep behavior identical for normal plugin startup
- avoid broad constructor rewrites or moving the plugin to a new framework
- keep runtime-specific code in the mock runner and mock-test rail, not
  scattered throughout the production plugin

## Testing Split After This Change

### `Echoglossian.Tests`

Keep this rail for:

- pure logic
- policy helpers
- caches and persistence helpers
- source-scope and routing logic
- translator selection logic
- contract-style tests that do not need a host

### DalaMock Rail

Use the DalaMock rail for:

- startup composition
- lifecycle registration and teardown
- service wiring around the plugin entrypoint
- future host-level smoke tests where unit doubles are awkward or misleading

This split is intentional. The two rails solve different problems.

## Failure Behavior

- If local validation fails in `Echoglossian.Tests`, the unit-test rail remains
  the source of truth for that failure.
- If local validation fails in `Echoglossian.Mock.Tests`, the DalaMock rail
  indicates a startup/wiring regression or an incorrect mock-host seam.
- If `Echoglossian.Mock` fails to launch manually, the mock runner should be
  treated as development tooling failure, not as a reason to change the D17
  deploy flow.
- If DalaMock integration proves too invasive for a given area, the default
  answer is to keep that area on the existing test rail rather than forcing a
  migration.

## Validation

The implementation itself must be validated locally only.

The repository-level validation entrypoint becomes:

```powershell
.\scripts\validate-local-tests.ps1
```

The implementation will also verify, as applicable:

- `pages.yml` still exists and remains the only active workflow in scope
- plugin-build GitHub workflows are no longer active
- `Echoglossian.sln` is unchanged
- `Echoglossian.Mock` builds locally
- `Echoglossian.Mock.Tests` builds and passes locally
- the production plugin still builds locally through the unchanged solution

## Non-Goals

This work will not:

- add `Echoglossian.Tests.csproj` to the solution
- add `Echoglossian.Mock` or `Echoglossian.Mock.Tests` to the solution
- create a new GitHub Actions validation workflow for the plugin
- keep GitHub-side plugin build validation in parallel with the local-only flow
- migrate the plugin to `HostedPlugin`
- replace `Echoglossian.Tests`
- rewrite the plugin into Autofac or a new DI container model
- migrate the existing unit tests wholesale into DalaMock
- change the D17 deployment mechanism

## Risks and Mitigations

- **Mock-host integration grows into architecture rewrite:** constrain the
  first DalaMock cut to startup and hook wiring only.
- **Local validation drift:** keep one canonical script instead of ad hoc
  command sequences.
- **Confusion about deploy authority:** remove plugin-build workflows from
  GitHub so the repository no longer suggests GitHub deploy ownership.
- **Duplicated test intent across rails:** document the split clearly and keep
  DalaMock tests focused on host/runtime seams.
- **Production behavior changes during seam extraction:** extract the smallest
  possible startup seam and reuse it from the current entrypoint rather than
  introducing alternate logic branches.

## Acceptance Criteria

- `Echoglossian.sln` remains unchanged.
- `Echoglossian.Tests` continues to run outside the solution through explicit
  local build/test commands.
- A canonical local validation script exists and runs the full explicit test
  sequence.
- GitHub plugin-build workflows are disabled by removing the active workflow
  files.
- `pages.yml` remains active.
- `Echoglossian.Mock` exists as a local DalaMock runner.
- `Echoglossian.Mock.Tests` exists as a complementary local test rail.
- The first DalaMock tests cover startup/service-wiring behavior, not a broad
  migration of the unit-test suite.
- The production plugin entrypoint remains `Echoglossian : IDalamudPlugin`.
- No GitHub workflow is introduced that builds or tests the plugin.

## References

- [Critical-Impact/DalaMock](https://github.com/Critical-Impact/DalaMock)
