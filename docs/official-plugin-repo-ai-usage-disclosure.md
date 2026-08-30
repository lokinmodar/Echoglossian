# Official Plugin Repository AI Usage Disclosure

This repo targets eventual submission and maintenance in the official Dalamud
plugin ecosystem, so AI usage needs an explicit disclosure workflow.

Reference policy:
- https://github.com/goatcorp/governance/blob/main/ai-policy-official-repo.md

## What This Policy Applies To

The Goatcorp policy applies to submissions for the official Dalamud plugin
repository.

The important repo-local implication is:
- if a PR for official distribution involved AI beyond basic autocomplete or
  inline suggestions, the PR description must disclose that level of AI use
- if AI-generated assets are included, they must also be disclosed

## Disclosure Levels

Use the Goatcorp levels verbatim when disclosure is required:

- `Assist`: human-led work with AI used for bounded tasks
- `Pair`: active human/AI collaboration throughout the work
- `Copilot`: AI wrote most of the code while the human planned and reviewed
- `Auto`: AI acted mostly autonomously with minimal human direction

No disclosure is required for:

- `None`
- `Hint` / basic autocomplete / inline suggestions only

## Repo Workflow

For this repo, the expected workflow is:

1. use the PR template in `.github/PULL_REQUEST_TEMPLATE.md`
2. fill in `AI Usage Disclosure` whenever the work went beyond autocomplete
3. disclose any AI-generated user-facing assets in the same PR
4. keep the disclosure concise but specific enough that a reviewer can tell how
   the work was produced and validated

For the official `DalamudPluginsD17` PR body, also follow
`docs/discord-webhook-safe-pr-format.md` so any Discord webhook mirror keeps
the disclosure readable. Prefer bold section labels, flat bullets, fenced
`text` blocks, and raw URLs over GitHub-only constructs such as task lists,
tables, masked links, HTML, or collapsible sections.

## Avoid Overstating Autonomy

These notes do not mean every official `DalamudPluginsD17` submission requires
AI disclosure, and they do not mean every disclosed submission should say
`Assist`.

The first step is still to determine whether disclosure is required at all
under the Goatcorp policy.

When disclosure is required, do not escalate the level to `Auto` solely because
an agent executed many intermediate implementation or release steps.

Human-scoped, human-reviewed, human-approved, and human-QA'd work is not
autonomous by itself. Use `Auto` only when the agent truly operated with
minimal human direction and minimal human review before the official
submission.

## Important Clarification For Echoglossian

Echoglossian can call machine-translation and LLM providers at runtime as part
of the plugin's user-facing feature set. That is separate from this governance
requirement.

The governance requirement is about:
- AI used during development of the plugin
- AI-generated assets shipped with the plugin

It is not a blanket ban on translation engines or runtime AI-backed
translation.

## Suggested Disclosure Snippet

Use this when a PR needs disclosure in the official D17 PR body:

```text
**AI Usage Disclosure**
`Assist`

AI scope:
- bounded implementation help
- code explanation / review assistance
- test drafting

Human verification:
- reviewed the final diff manually
- ran local build and tests
- performed in-game verification where applicable
```

If the human also controlled issue triage, plan approval, release approval, or
QA, say so explicitly instead of overstating autonomy.

## Asset Disclosure Reminder

If AI-generated assets are ever used, the Goatcorp policy expects that to be
declared in the plugin's description as well because those assets are directly
user-facing.
