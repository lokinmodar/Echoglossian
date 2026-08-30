# Discord-WebHook-Safe D17 PR Format

This repo sometimes mirrors official `DalamudPluginsD17` pull request text into
Discord webhook messages. Write those PR bodies so they still read correctly in
GitHub and in Discord.

Verified on `2026-08-30` against:

- Discord API Reference, Message Formatting:
  https://docs.discord.com/developers/reference
- Discord Support, Markdown Text 101:
  https://support.discord.com/hc/en-us/articles/210298617-Markdown-Text-101-Chat-Formatting-Bold-Italic-Underline
- GitHub Docs, task lists:
  https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/about-tasklists
- GitHub Docs, tables:
  https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/organizing-information-with-tables

## Discord-Supported Message Syntax

Discord documents support for message-content formatting such as:

- plain paragraphs and line breaks
- bold, italic, underline, and strikethrough
- list markers
- inline code and fenced code blocks
- block quotes
- headers, subtext, and masked links

## Repo-Approved Subset For D17 PR Bodies

Even though Discord supports more than this, official D17 PR text should use
only the smaller subset below so webhook mirrors stay predictable:

- plain paragraphs
- bold section labels such as `**Summary**`
- flat `-` bullet lists
- flat numbered lists when order matters
- inline code for versions, refs, file paths, and identifiers
- fenced `text` code blocks for disclosure snippets
- full raw URLs for issue, PR, and release links

Use full raw URLs instead of masked links for cross-surface references. That
keeps the destination visible in GitHub and avoids depending on whichever
Discord webhook payload shape is forwarding the text.

## Syntax To Avoid In D17 PR Bodies

Do not use these in the official `DalamudPluginsD17` PR text:

- GitHub task lists such as `- [ ]` and `- [x]`
- Markdown tables
- HTML blocks
- `<details>` / collapsible sections
- images or screenshots embedded in the PR body
- reference-style links
- masked links such as `[issue #274](https://...)`
- deep or nested list structures
- Discord mentions, slash-command syntax, spoilers, or timestamps unless the
  submission explicitly needs them

## Canonical D17 PR Template

Use this body shape for future official D17 submissions:

````text
**Summary**
- update `stable/Echoglossian` to `vX.Y.Z`
- this pulls in the merged fix or release already published from `v4-series`
- concise fix description in 1-3 bullets

**Validation**
- local release build completed
- targeted tests completed

**Source Links**
- Echoglossian PR: https://github.com/lokinmodar/Echoglossian/pull/NNN
- Release tag: https://github.com/lokinmodar/Echoglossian/releases/tag/vX.Y.Z
- Issue: https://github.com/lokinmodar/Echoglossian/issues/NNN

**AI Usage Disclosure**
`Assist` | `Pair` | `Copilot` | `Auto` | `None`

```text
AI scope:
- tooling used:
- human direction/review:
- verification:
```

**AI-Generated Assets Disclosure**

```text
AI-generated assets:
- none
```
````

## Repo Script

Generate this body with the repo-local helper instead of freehand Markdown when
possible:

```powershell
.\scripts\new-d17-pr-body.ps1 `
  -Version "vX.Y.Z" `
  -EchoglossianPrUrl "https://github.com/lokinmodar/Echoglossian/pull/NNN" `
  -ReleaseTagUrl "https://github.com/lokinmodar/Echoglossian/releases/tag/vX.Y.Z" `
  -IssueUrl "https://github.com/lokinmodar/Echoglossian/issues/NNN" `
  -SummaryLine "pull in the merged fix from v4-series" `
  -ValidationLine "local release build completed"
```

The helper intentionally emits only the repo-approved subset and rejects common
GitHub-only constructs such as task-list bullets and masked links in
user-supplied lines.

## Practical Rule

If a formatting feature is documented by GitHub but not clearly documented by
Discord message formatting, do not use it in the official D17 PR text.
