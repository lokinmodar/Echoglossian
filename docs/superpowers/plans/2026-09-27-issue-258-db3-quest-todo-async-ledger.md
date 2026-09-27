# DB-3 Subagent Ledger

Merge base: `77949329c2671f353585763159bcec4d433ef983`

Task 0: critical interface review complete. Ruling: introduce one QuestPlate projection/state registry, not a second queue, because coordinator write coalescing ends on claim. Cost if wrong: the registry could mis-key lookup variants; Task 1's parity tests and per-task review must reject that outcome.

Task 1: review failed (Critical: legacy policy divergence; no terminal cooldown; stale cancellation generation; Important: write bypasses registry; insufficient contract tests). Fix round 1/5 pending; commits 6863e97..d29efee.

Task 1 fix round 4: the shared QuestPlate registry now preserves a same-key
upsert that joins an active read by reserving one deferred writer completion,
then atomically promoting it after the read terminal state. Legacy merge writes
again commit `UpdatedDate` even when only that timestamp changes. Focused
SQLite/cache regression: 9/9 passed (`QuestPlatePersistenceWriterTests`);
the commit-only timestamp projection and read-then-write serialization are
covered.
