# Issue 258 Async Persistence Baseline

## Evidence Status

Reference documents:

- Issue: https://github.com/lokinmodar/Echoglossian/issues/258
- [Master design](superpowers/specs/2026-09-01-issue-258-async-persistence-and-translation-toggle-design.md)
- [Current synchronous DB inventory](issue-258-sync-db-hotpath-inventory.md)

The Issue 258 report observed repeated drops from approximately 110 FPS to
87 FPS, accepted-quest prefetch bursts approximately every two seconds, and
SQLite WAL write activity approximately every second. These are reporter
observations, not a controlled benchmark.

## Controlled Scenario

1. Use the same Debug build, character, territory, target language, and
   translator for the before/after pair.
2. Enable global translation plus `TranslateActionMenuWindow`,
   `TranslateMainCommandWindow`, or `TranslateTooltips` so reference-text
   prefetch runs.
3. Enable at least one of `TranslateJournal`, `TranslateJournalDetail`,
   `TranslateToDoList`, `TranslateScenarioTree`, `TranslateRecommendList`, or
   `TranslateAreaMap` so accepted-quest prefetch runs.
4. Start with at least five accepted quests and a warm game session, then
   observe for two uninterrupted minutes without changing configuration.
5. Repeat once with a warm translation database and once with the targeted
   rows removed from a disposable database copy.

## Required Capture

- median, p95, and p99 frame time;
- observed FPS range;
- SQLite WAL write frequency and busy/retry count;
- persistence queue maximum depth and oldest-item age when those counters
  become available in DB-1;
- batch count, written-row count, and unchanged-row suppression count;
- timestamped excerpts from `Echoglossian.log` and
  `accepted-quest-prefetch-activity.log`.

## Comparison Rule

DB-2 and every later performance release append one dated before/after result
using this exact scenario. A result is not comparable if configuration,
translator, character quest set, observation duration, or database warmth
changes between the two captures.

## Logging Rule

Use summarized counters and lifecycle boundaries. Do not add per-frame or
per-row production logs to obtain the measurements.

## DB-2 Functional Validation - 2026-09-06/07 BRT

This session validated behavior but was not a controlled before/after
performance capture under the comparison rule above.

- Build SHA-256:
  `AE86FFC9FA436611C2492287D108027C03D02001EE1373DF767E187808A84349`.
- Translator observed in the session log: Google Translator.
- `Echoglossian.log` records accepted-quest and ReferenceText-adjacent activity
  through `2026-09-06T23:33:50-03:00`, followed by plugin-owned cache teardown
  beginning at `2026-09-06T23:33:54-03:00`.
- `Echoglossian.db-wal` was last written at `2026-09-06 23:33:50-03:00`, showing
  that the unload test occurred immediately after active persistence traffic.
- The first Test 5 attempt before owner-lifetime ReferenceText cancellation
  froze indefinitely on immediate re-enable. After the correction, the user
  repeated Test 5 and reported no unload/reload errors.

The following required comparison values were not emitted by the available
logs and therefore remain pending: median/p95/p99 frame time, comparable FPS
range, queue maximum depth and oldest-item age, batch and written-row counts,
unchanged-row suppression count, SQLite busy/retry count, and comparable WAL
write frequency. No performance claim is made from this functional session.
