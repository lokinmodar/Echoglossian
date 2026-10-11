## Upstream

This folder vendors the minimum DalaMock source needed by `Echoglossian.Mock` and
`Echoglossian.Mock.Tests`.

- Upstream repository: https://github.com/Critical-Impact/DalaMock
- Upstream tag: `6.2.0`
- Upstream commit: `1eb468494f9bc5f123b54a5c5d20c3fd182f8ca9`
- Upstream tag date: October 3, 2026

## Local Patch

This vendored copy currently carries one narrow compatibility fix required by
the hosted preview and mock rails:

1. `DalaMock/Plugin/PluginLoader.cs`
   - hosted startup that uses an async adapter plus
     `PluginLoadSettings.AssemblyLocation` can resolve the wrong plugin assembly
     identity
   - the local patch prefers the explicit assembly location and avoids adapter
     base-type resolution that can collapse to `System.Private.CoreLib`

Upstream 6.2.0 supplies the `MockFramework.CreateDebouncer` implementation and
the corresponding `Debouncer` type, so this vendor copy retains no local
debouncer shim.

Apart from that functional delta, vendored comments and documentation may have
punctuation normalized to comply with this repository's no-U+2014 policy.

## Upstream Intent

The remaining fix is a good upstream candidate because it corrects DalaMock's
compatibility with hosted-plugin loading patterns. See
`docs/dalamock-hosted-preview-boundary.md` for the boundary between upstream
DalaMock fixes and Echoglossian-specific hosting code.

Revisit this folder when upstream releases a version that includes equivalent
fixes so the vendored copy can be reduced or removed.
