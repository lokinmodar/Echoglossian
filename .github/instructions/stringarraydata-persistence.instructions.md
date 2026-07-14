---
description: "Use when editing StringArrayDatas persistence, cache, or DB helpers in Echoglossian."
applyTo:
  - "DBHelpers/StringArrayDataPersistenceHelper.cs"
  - "Cache/StringArrayDataCacheManager.cs"
  - "EFCoreSqlite/Models/StringArrayDatas*.cs"
---

# StringArrayData persistence and cache

- The canonical row owns persistence; caches only suppress repeated work.
- Keep structured payload rows stable and do not re-key them by visible text alone.
- Preserve original payload shape in the DB and avoid flattening or fragmenting rows.
- Keep live-progress or runtime keys separate from persistent storage keys.
- Use cooldown behavior and cache lookups to avoid frame-by-frame translation churn.
- Keep additive migrations and compatibility with the current `StringArrayDatas` model unless a reset is explicitly requested.
- Canonical rows include source identity and reuse through
  `TranslationReuseScope`; source, target, content/version, and engine policy
  remain required predicates.
- Do not run a database-wide alias rewrite or implicit deduplication. A scoped
  upsert may promote the compatible row it updates to canonical source metadata;
  blank, generic, unknown, and ambiguous Chinese origins remain non-reusable.
