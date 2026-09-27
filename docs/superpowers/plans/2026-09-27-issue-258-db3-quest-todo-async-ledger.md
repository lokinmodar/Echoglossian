# DB-3 Subagent Ledger

Merge base: `77949329c2671f353585763159bcec4d433ef983`

Task 0: critical interface review complete. Ruling: introduce one QuestPlate projection/state registry, not a second queue, because coordinator write coalescing ends on claim. Cost if wrong: the registry could mis-key lookup variants; Task 1's parity tests and per-task review must reject that outcome.
