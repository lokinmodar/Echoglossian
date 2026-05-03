# Translation Engine Backlog

This document tracks translation-engine candidates that were requested but are
not part of the active implementation scope yet.

The purpose is to capture fit, risks, and architectural impact before anyone
starts wiring a new engine into the plugin.

## Microsoft 365 Copilot Chat API

Status: future backlog candidate

Reference:
- https://github.com/MicrosoftDocs/m365copilot-docs/blob/main/docs/api/ai-services/chat/overview.md

Why it was requested:
- external request to evaluate Microsoft 365 Copilot as another LLM-backed
  translation engine option

What the official docs currently say:
- the API is currently in preview
- it supports multi-turn conversations with Microsoft 365 Copilot
- responses are grounded with enterprise search and optionally web search
- it returns textual responses only
- it does not support tool use or content-generation skills
- it is available to users with a Microsoft 365 Copilot add-on license
- support for users without a Microsoft 365 Copilot add-on license is not
  currently available

Why this is not a drop-in engine:
- it is not another OpenAI-compatible chat endpoint
- the product model is tenant- and license-bound rather than just key-bound
- its primary value proposition is enterprise/work-data grounding, not generic
  low-friction LLM inference
- Echoglossian would need to decide whether this engine is meant to translate
  plain game text or whether enterprise grounding makes it a poor fit for the
  plugin's runtime translation workload

Known integration concerns:
- licensing and availability may make it unusable for most plugin users
- preview status means API shape and constraints can still move
- grounding defaults and Microsoft 365 trust-boundary behavior do not map
  cleanly to the plugin's current "send text, get translation" engine pattern
- if authentication depends on Microsoft identity / Graph-style delegated auth,
  the plugin UI and config model would need a different setup from today's
  API-key-first LLM engines
- translation quality, latency, and rate-limit behavior for short repetitive
  game UI strings are still unknown

Architecture impact if pursued later:
- likely needs its own concrete translator rather than reuse of
  `ChatGPTTranslator`, `OpenRouterTranslator`, `DeepSeekTranslator`, or
  `ClaudeTranslator`
- likely needs dedicated config UI and auth flow
- may need separate capability gating in `LanguageEngineSupport` depending on
  what languages and usage model Microsoft exposes
- should be documented and reviewed as a distinct engine family, not as a minor
  variant of existing LLM engines

Minimum future investigation checklist:
1. confirm auth flow and whether a normal plugin user can configure it without
   tenant admin friction
2. confirm whether the API can be used for straightforward translation prompts
   without undesirable enterprise-search grounding
3. confirm latency, quotas, and pricing/licensing behavior for plugin-scale use
4. confirm whether the API can be safely represented as another selectable
   engine in the current config UX
5. decide whether this belongs in the broad-coverage LLM bucket or in a more
   restricted engine-availability path
