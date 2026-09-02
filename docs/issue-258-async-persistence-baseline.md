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
