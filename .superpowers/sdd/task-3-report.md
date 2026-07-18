# Task 3 Report

## Status

Implemented and committed the reusable DalaMock hosted-session bootstrap.

## Changes

- Added `HostedPreviewPluginOptions`, `HostedPreviewPluginSession`, and `HostedPreviewPluginSessionFactory` under `Echoglossian.Mock/Hosting`.
- Moved the runner onto the shared factory with runner-owned state, save, and config paths.
- Made the default `TestBoot` startup path delegate to the shared factory while retaining its injected failure seams for existing cleanup coverage.
- Added the hosted-session smoke test and the required mock-project test reference; `FluentAssertions` was already present, so no package was added.

## TDD Evidence

- RED: the focused test initially failed to compile because `Echoglossian.Mock.Hosting` did not exist.
- GREEN attempt: after adding the hosted API, the test reached DalaMock startup.

## Validation

- `dotnet restore Echoglossian.Mock\\Echoglossian.Mock.csproj` succeeded with existing vulnerability warnings.
- `dotnet restore Echoglossian.Mock.Tests\\Echoglossian.Mock.Tests.csproj` succeeded with existing vulnerability warnings.
- `dotnet build Echoglossian.Mock\\Echoglossian.Mock.csproj -c Debug --no-restore` succeeded.
- `dotnet build Echoglossian.Mock.Tests\\Echoglossian.Mock.Tests.csproj -c Debug --no-restore` succeeded.
- `git diff --check` succeeded.
- Focused smoke test is blocked before hosted-session code executes: `DalaMock.Core` 6.1.7 throws `ReflectionTypeLoadException` because `MockFramework.CreateDebouncer` has no implementation against the installed Dalamud assemblies.

## Self-Review

- The factory uses explicit supplied state, plugin-save, and config paths and exposes those same instances through the session.
- Normal plugin packaging and release files were not changed.
- The only runtime behavior changed is the local DalaMock runner and test bootstrap being routed through the reusable factory.
- The `DatabasePath` option is retained exactly as specified for the later preview backend and is intentionally not consumed by this task's existing DalaMock startup path.
