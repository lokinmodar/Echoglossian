# Localization Incident Post-Mortem Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Document the August 2026 Crowdin data-integrity and runtime resource-culture incident and make its preventive controls part of the repository's permanent localization workflow.

**Architecture:** Create one evidence-based incident record that separates the Crowdin synchronization failure from the runtime resource-loading failure while showing their shared control gaps. Add only the operational guardrails to the existing localization build guide so maintainers encounter them during normal work.

**Tech Stack:** Markdown, Git history, Crowdin project audit evidence, .NET RESX/ResourceManager terminology, repository-local validation commands.

## Global Constraints

- Keep `Properties/Resources.resx` as the only English source of truth.
- Keep localized output at `Properties/Resources.%locale%.resx`.
- Do not alter Crowdin translations or RESX content while writing the incident record.
- Distinguish verified facts from recommendations and unresolved follow-up work.
- Keep the account blameless and never include credentials or tokens.

---

### Task 1: Create the incident record

**Files:**
- Create: `docs/localization-crowdin-runtime-postmortem-2026-08.md`

**Interfaces:**
- Consumes: Git commits `a7ba2c6`, `bec0043`, `f93c46e`; PRs #253 and #260; Crowdin recovery audit results from 2026-08-07.
- Produces: Canonical incident timeline, root causes, recovery record, corrective-action register, safe runbook, and rollback procedure.

- [x] **Step 1: Record the incident scope and impact**

Describe the Crowdin approval corruption and runtime fallback-to-English behavior as separate failure tracks with one shared localization migration context.

- [x] **Step 2: Record the evidence-backed timeline and root causes**

Include the locale rename, Crowdin configuration change, English fallback export/import, recovery, and runtime fix chronology. Identify the missing `Resources.Culture` assignment and missing `ca`/`nl` mappings.

- [x] **Step 3: Record recovery and prevention controls**

Include completed Crowdin settings, restored approval counts, PR #260, mandatory synchronization rules, diff review gates, runtime checks, and open automation work with acceptance criteria.

- [x] **Step 4: Add rollback and verification procedures**

Document how to stop synchronization, preserve historical variants/TM, restore from a known-good Git commit, audit approvals, and verify satellite assemblies and resource lookup.

### Task 2: Put guardrails in the daily workflow

**Files:**
- Modify: `docs/localization-build-flow.md`

**Interfaces:**
- Consumes: The canonical incident record from Task 1.
- Produces: A concise pre-sync, PR-review, build, and runtime checklist maintainers encounter during ordinary localization work.

- [x] **Step 1: Add Crowdin integration safety rules**

State that continuous translation import and manual `Sync Translations to Crowdin` are prohibited after Crowdin becomes the source of truth.

- [x] **Step 2: Add generated-PR review gates**

Require rejection of mass source-equivalent replacements, identical localized files, unexpected locale additions, and large approval-count regressions.

- [x] **Step 3: Add runtime culture validation**

Require culture normalization, `Resources.Culture` application, exact satellite assembly presence, and a localized lookup test.

### Task 3: Review and publish

**Files:**
- Review: `docs/localization-crowdin-runtime-postmortem-2026-08.md`
- Review: `docs/localization-build-flow.md`
- Review: `docs/superpowers/plans/2026-08-08-localization-incident-postmortem.md`

**Interfaces:**
- Consumes: Documentation produced by Tasks 1 and 2.
- Produces: A committed and pushed documentation update on PR #260.

- [x] **Step 1: Run the documentation self-review**

Search for placeholders, contradictions, ambiguous ownership, unsupported claims, missing links, and accidentally included secrets.

- [x] **Step 2: Run repository checks**

Run:

```powershell
git diff --check
node .\Echoglossian.Docs\scripts\check-locales.mjs
```

Expected: no diff errors; locale compatibility check exits successfully.

- [x] **Step 3: Commit and push**

```powershell
git add -- docs/localization-crowdin-runtime-postmortem-2026-08.md docs/localization-build-flow.md docs/superpowers/plans/2026-08-08-localization-incident-postmortem.md
git commit -m "docs: add localization incident post-mortem"
git push
```
