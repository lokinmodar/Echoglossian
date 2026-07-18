# Task 6 Report: End-To-End Safety And No-Plugin-Break Guard

## Status

DONE_WITH_CONCERNS

## Documentation

- Updated `Echoglossian.Previewer/README.md` with CLI and shell backend
  selection, backend-specific behavior, manifest metadata, and fallback
  troubleshooting.
- Updated `docs/handoffs/unified-imgui-previewer-next-step.md` with Phase 2
  shipped behavior, validation evidence, and the remaining DalaMock debt.
- Did not update the approved design spec: implementation remains consistent
  with its hybrid backend, visible fallback, manifest metadata, and isolation
  contracts.
- Did not regenerate `Echoglossian.xml`.

## Validation Evidence

All commands ran from
`C:\Dante\_dalamud\worktrees\Echoglossian\previewer-dalamock-font-builds`.

| Command | Outcome |
| --- | --- |
| `dotnet build Echoglossian.sln -c Debug --no-restore` | Passed: `0` errors, `76` existing warnings. Previewer and mock projects remain outside the solution. |
| `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1` | Passed: `642 / 642`. |
| `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore -p:VSTestMaxCpuCount=1` | Passed: `143 / 143`. |
| `dotnet restore Echoglossian.Mock\Echoglossian.Mock.csproj` | Passed. |
| `dotnet restore Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj` | Passed. |
| `dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore` | Passed: `0` errors, `9` warnings. |
| `dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1` | Failed: `5 / 11` due to the known DalaMock hosted-startup blocker. |
| `dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --host-smoke` | Passed: exit `0`. |
| Auto backend Config capture command from the task brief | Passed: wrote `artifacts\previewer\hosted-backend-validation\manifest.json` and `full-talk-configwindow-1920x1080.png`. |

## DalaMock Blocker

The failing DalaMock tests throw `System.Reflection.ReflectionTypeLoadException`
while `DalaMock.Core.Plugin.MockContainer` registers mock services. The loader
reports:

```text
Method 'CreateDebouncer' in type 'DalaMock.Core.Mocks.DalamudServices.MockFramework'
from assembly 'DalaMock.Core, Version=6.1.7.0, Culture=neutral,
PublicKeyToken=null' does not have an implementation.
```

The auto-backend capture exercised this same failure safely. It succeeded by
recording `RequestedPluginWindowBackend: Auto`,
`EffectivePluginWindowBackend: Standalone`, and the `CreateDebouncer` failure
in `PluginWindowBackendFallbackReason`. This confirms that the fallback guard
preserves the standalone previewer path and makes the limitation visible.

## Scope

No plugin packaging, release-flow, solution-membership, or production-plugin
code changes were made in Task 6.
