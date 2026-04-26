# Action-adjacent reference text sheet flow

This flow owns reusable action-adjacent sheet text that can be reused by
`ActionMenu` and future surfaces without depending on the live addon payload as
the source of truth.

## Active families

- `GeneralActionText`
- `BuddyActionText`
- `CompanyActionText`
- `CraftActionText`
- `PetActionText`
- `EventActionText`
- `BgcArmyActionText`
- `AozActionText`
- `PvPActionText`
- `MountActionText`
- `EurekaMagiaActionText`
- `MainCommandText`

## Code paths

- runtime tick:
  [PluginRuntimeUi.cs](/C:/Dante/_dalamud/Echoglossian/PluginUI/PluginRuntimeUi.cs)
- prefetch runtime:
  [ReferenceTextPrefetchRuntime.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/ReferenceTextPrefetchRuntime.cs)
- canonical payload:
  [ReferenceTextCanonicalPayload.cs](/C:/Dante/_dalamud/Echoglossian/NativeUI/Helpers/ReferenceTextCanonicalPayload.cs)
- persistence:
  [ReferenceTextPersistenceHelper.cs](/C:/Dante/_dalamud/Echoglossian/DBHelpers/ReferenceTextPersistenceHelper.cs)
  [ReferenceTextDbOperations.cs](/C:/Dante/_dalamud/Echoglossian/DBHelpers/ReferenceTextDbOperations.cs)
- cache registry:
  [ReferenceTextCacheRegistry.cs](/C:/Dante/_dalamud/Echoglossian/Cache/ReferenceTextCacheRegistry.cs)
  [ReferenceTextCacheStore.cs](/C:/Dante/_dalamud/Echoglossian/Cache/ReferenceTextCacheStore.cs)

## Data flow

```text
Excel sheet rows
  -> ReferenceTextPrefetchRuntime
  -> ReferenceTextCanonicalPayload
  -> specific row factory per family
  -> specific DB table
  -> specific ReferenceText cache
  -> aggregated cache registry lookups
  -> consumers such as ActionMenu
```

## `MainCommand` placement

- `MainCommand` belongs to this sheet-backed canonical flow as `MainCommandText`.
- That does not migrate the live `_MainCommand` addon runtime.
