## Upstream

This folder vendors the minimum DalaMock source needed by `Echoglossian.Mock` and
`Echoglossian.Mock.Tests`.

- Upstream repository: https://github.com/Critical-Impact/DalaMock
- Upstream tag: `6.1.7`
- Upstream commit: `2fd273393d55c9850bfce2314520a50254954b12`
- Upstream tag date: June 7, 2026

## Local Patch

This vendored copy currently carries one narrow compatibility fix required by
the hosted preview and mock rails:

1. `DalaMock/Plugin/PluginLoader.cs`
   - hosted startup that uses an async adapter plus
     `PluginLoadSettings.AssemblyLocation` can resolve the wrong plugin assembly
     identity
   - the local patch prefers the explicit assembly location and avoids adapter
     base-type resolution that can collapse to `System.Private.CoreLib`

The prior local `MockFramework.CreateDebouncer` patch was removed because stable
Dalamud 15.0.2.3 does not expose `IDebouncer`; keeping that staging-only member
breaks builds against stable Dalamud binaries.

## Upstream Intent

The remaining fix is a good upstream candidate because it corrects DalaMock's
compatibility with hosted-plugin loading patterns. See
`docs/dalamock-hosted-preview-boundary.md` for the boundary between upstream
DalaMock fixes and Echoglossian-specific hosting code.

Revisit this folder when upstream releases a version that includes equivalent
fixes so the vendored copy can be reduced or removed.
