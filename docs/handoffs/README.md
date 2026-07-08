# Workstream Handoffs

Snapshot date: 2026-07-07

This folder exists to make future chats resume work from repo state instead of
depending on long thread history.

Read `AGENTS.md` first. Then use the handoff that matches the branch or issue
you want to resume.

## Current repo snapshot

- current branch: `v4-series`
- current `v4-series` head: `e44805c` (`docs: refresh open issue backlog`)
- local worktree is dirty:
  - `Echoglossian.xml`
- treat the current `Echoglossian.xml` change as unrelated until it is
  intentionally validated and committed

## Main active fronts

| Front | Branch | GitHub PR | Status |
| --- | --- | --- | --- |
| JournalDetail native reflow and quest-mode safety | `issue-181-journaldetail-reflow` | `#193` | draft, intentionally isolated from `v4-series` |
| LLM translation rework | `llm-translation-rework` | `#202` | open, large branch, unresolved review debt remains |
| Open release regressions / tracker triage | `v4-series` plus focused issue branches | n/a | backlog and issue-driven work |

## Suggested resume flow for a new chat

1. Tell the new chat which front you want.
2. Point it at the handoff in this folder.
3. Tell it which branch to use.
4. Ask it to verify branch state against the repo before editing.

Example:

> Continue from `docs/handoffs/journaldetail-quest-native-reflow.md` on
> `issue-181-journaldetail-reflow`.

## Handoffs in this folder

- [JournalDetail And Quest Native Reflow](./journaldetail-quest-native-reflow.md)
- [LLM Translation Rework](./llm-translation-rework.md)
- [Open Regression Cluster](./open-regression-cluster.md)

## Shared repo rules worth repeating

- Do not incubate unstable issue work on `v4-series`.
- Keep issue work on issue branches and merge back by PR.
- If the selected translation mode does not mutate native UI, do not touch
  native nodes.
- For user-facing plugin UI strings and notifications, use `Resources.Key`
  directly.
- Commit `Echoglossian.xml` when it changes as part of a validated code change.
