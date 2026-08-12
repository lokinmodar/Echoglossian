# LLM Capability Matrix Design

**Date:** 2026-08-12

**Repository:** `lokinmodar/Echoglossian`

## Objective

Add one conservative, shared LLM capability matrix that drives both engine
configuration UI and runtime payload sanitization so Echoglossian stops sending
model-incompatible parameters and only exposes supported controls for the
selected LLM model.

## Problem

Recent logs showed multiple classes of provider incompatibility that the current
architecture does not model centrally:

- some OpenAI reasoning-model chat-completions paths reject
  `reasoning_effort` when combined with tool-based structured dialogue;
- some OpenAI models reject non-default `temperature` values;
- Claude 4.7+ and later models reject non-default sampling parameters;
- Gemini is deprecating sampling parameters for newer model generations;
- local or compatibility endpoints may differ by host and model family.

Today those constraints are not represented in one shared policy layer.
Instead:

- the UI shows generic controls such as `Temperature` with one static range;
- translators often send configured parameters unconditionally;
- live model refresh only discovers model identities, not request-shape
  compatibility;
- fixes risk being scattered across individual translators and drifting apart.

That creates three user-facing failures:

1. invalid requests reach providers and fail at runtime;
2. the configuration UI offers controls that are not actually valid for the
   selected model;
3. one provider-specific hotfix can leave other LLM translators inconsistent.

## Goals

- Add one shared capability-policy layer for LLM engines only:
  - OpenAI / ChatGPT and custom OpenAI-compatible
  - Claude
  - Gemini
  - DeepSeek
  - OpenRouter
  - Ollama
  - LM Studio
- Drive both UI gating and runtime payload sanitization from the same effective
  policy resolution path.
- Keep the behavior conservative:
  - if support is unknown, the runtime omits the parameter;
  - the UI shows the control disabled with a tooltip rather than pretending it
    is supported.
- Reuse the existing live-model refresh flow as the single operator entrypoint
  for refreshing both model identity and capability overlays.
- Store live and learned capability overlays in SQLite, not in user config.
- Support exact-model rules and family-prefix inheritance.
- Allow bounded auto-learning from clearly classifiable provider `400` errors.

## Non-Goals

- No change to non-LLM translation engines.
- No second model-discovery pipeline or second refresh button.
- No replacement of current engine-specific translator classes.
- No automatic promotion from one observed model error to an entire family.
- No optimistic assumption that a successful request proves a parameter is
  universally supported.
- No provider-body dumps, raw prompts, or credential material in logs.
- No broad refactor of all model catalog code in this first slice.

## Existing Foundations

The repository already has the right architectural seams for this work:

- static model defaults already exist per engine family;
- live model refresh already exists through
  `PluginUI/EngineConfigUI/LiveModelRefreshCoordinator.cs`;
- live model identity is already tracked by engine-specific model managers such
  as:
  - `Translators/OpenAI/OpenAIModelManager.cs`
  - `Translators/Claude/ClaudeModelManager.cs`
  - `Translators/Gemini/GeminiModelManager.cs`
- engine configuration already flows through dedicated UI surfaces and shared
  dropdown helpers such as
  `PluginUI/Components/ModelDropdownUI.cs`;
- SQLite persistence already exists through
  `EFCoreSqlite/EchoglossianDbContext.cs`.

This design extends those foundations instead of creating a parallel discovery,
configuration, or translation path.

## Recommended Architecture

Introduce one shared `LLM capability policy` layer between config/UI and the
translator request builders.

The layer has three pieces:

1. `Static defaults in code`
   - committed conservative capability defaults per LLM engine family;
   - includes exact-model and family-prefix rules;
   - available even before any live refresh runs.

2. `DB overlay`
   - additive SQLite-backed rules discovered or learned at runtime;
   - stores live capability overrides and observed provider incompatibilities;
   - separate from preferences so config remains user-owned settings only.

3. `Effective policy resolver`
   - computes one effective capability snapshot for a selected engine/provider
     scope/model;
   - used by both UI and runtime;
   - always resolves conflicts conservatively.

The same resolver output must be used in two places:

- `UI`: determine whether a control is enabled, its valid range, and its
  tooltip explanation;
- `Runtime`: sanitize outgoing request payloads so unsupported parameters are
  omitted before the provider request is sent.

## Scope Identity

Capability rules must be scoped more narrowly than engine name alone.

The effective lookup identity should include:

- `engine`
- `providerScope`
- `endpointScope`
- `modelId`

Where:

- `engine` is the plugin engine family such as `ChatGPT`, `Claude`, or
  `Gemini`;
- `providerScope` distinguishes semantically different providers inside one
  engine family, for example:
  - `OpenAI`
  - `OpenAI-Compatible`
  - `OpenRouter`
- `endpointScope` is a normalized base URL or endpoint identity when the same
  provider family can behave differently by host, especially for:
  - custom OpenAI-compatible endpoints
  - Ollama hosts
  - LM Studio hosts
- `modelId` is the exact selected model id.

This keeps official OpenAI behavior separate from custom compatibility servers,
and local-host behavior separate across installations.

## Data Model

Use two persisted entities.

### 1. `LlmModelCapabilityRule`

This is the effective-rule store used by the resolver.

Representative fields:

```csharp
public sealed class LlmModelCapabilityRule
{
    public long Id { get; set; }

    public string Engine { get; set; } = string.Empty;
    public string ProviderScope { get; set; } = string.Empty;
    public string EndpointScope { get; set; } = string.Empty;

    public string MatchType { get; set; } = string.Empty; // ExactModel or FamilyPrefix
    public string MatchValue { get; set; } = string.Empty;

    public string ParameterName { get; set; } = string.Empty;
    public string SupportState { get; set; } = string.Empty; // Supported, Unsupported, Unknown

    public float? MinValue { get; set; }
    public float? MaxValue { get; set; }
    public string AllowedEnumValuesJson { get; set; } = string.Empty;
    public bool OmitWhenDefaultOnly { get; set; }

    public string Source { get; set; } = string.Empty; // StaticDefault, LiveRefresh, Observed400, ManualPromotion
    public string Reason { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;

    public DateTime ObservedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
```

The first implementation only needs to cover the parameters currently exposed
or already causing real failures, such as:

- `temperature`
- `top_p`
- `top_k`
- `presence_penalty`
- `frequency_penalty`
- `reasoning_effort`
- structured-output mode or tool-calling compatibility where needed for the
  existing dialogue flows

### 2. `LlmModelCapabilityObservation`

This is a short audit trail for provider feedback that may promote exact-model
rules later.

Representative fields:

```csharp
public sealed class LlmModelCapabilityObservation
{
    public long Id { get; set; }

    public string Engine { get; set; } = string.Empty;
    public string ProviderScope { get; set; } = string.Empty;
    public string EndpointScope { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;

    public string ParameterName { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string ProviderErrorCode { get; set; } = string.Empty;
    public string MessageExcerpt { get; set; } = string.Empty;
    public DateTime ObservedAtUtc { get; set; }
}
```

Observations are not the runtime lookup source. They exist to support safe
promotion and postmortem review.

## Static Capability Catalog

The committed-in-code catalog should remain the default authority when the DB is
empty or stale.

It should be structured enough to express:

- exact model ids such as `gpt-5.6-terra`;
- family prefixes such as `gpt-5.6-`, `claude-sonnet-`, or `gemini-3`;
- parameter support states;
- numeric ranges;
- `default-only` semantics where a provider only accepts omission or one
  implicit default.

The first cut should keep this catalog in code rather than introducing a new
generator or external JSON authority. The shape should still be explicit enough
that a later migration to a generated catalog is straightforward.

## Effective Resolution Rules

The resolver computes one effective capability snapshot using this precedence:

1. static engine default
2. DB family-prefix overlay
3. DB exact-model overlay

Conflict rules:

- more specific beats less specific;
- DB overlay beats static default at the same specificity;
- if competing rules remain ambiguous, the effective result must choose the more
  conservative interpretation.

Conservative interpretation means:

- `Unsupported` beats `Unknown`;
- `Unknown` beats unconditional `Supported`;
- narrower numeric ranges beat broader ones;
- `default-only` means omit from runtime payload unless the current provider
  contract explicitly requires the value to be sent.

If nothing proves support, runtime behavior is omission.

## Live Refresh Behavior

`Fetch Live Models` remains the only operator-facing refresh entrypoint.

When live refresh runs, it may update two things:

1. the current discovered model list;
2. any provider metadata that can safely refine capability rules.

Live refresh is best-effort and must not assume every provider returns complete
capability metadata. Therefore:

- if a provider only returns model ids, only model identity is refreshed and
  static/family defaults continue to govern capability;
- if provider metadata is available, the refresh may write more specific rule
  overlays into SQLite;
- refresh failure must never wipe known-good static defaults or previously
  retained compatible overlays;
- refresh work remains asynchronous and owned by the existing refresh
  coordinator, with no callback blocking.

## UI Behavior

The configuration UI continues to show the relevant parameter controls, but they
become policy-aware.

For the selected engine/provider/model:

- unsupported controls remain visible but disabled;
- a tooltip explains why the control is disabled and where the rule came from,
  for example:
  - unsupported by the selected model
  - default-only for this model family
  - unknown capability, omitted conservatively
- when a valid numeric range is known, the UI uses that actual range instead of
  a one-size-fits-all slider;
- existing saved config values are not silently discarded, but the runtime may
  ignore them when incompatible.

This preserves operator visibility while preventing invalid edits.

## Runtime Sanitization

Every LLM translator request path must resolve the effective capability snapshot
before building its outbound payload.

The sanitization path must be shared logic, not ad hoc per translator, so that:

- plain-text and structured-dialogue requests both obey the same rule set;
- OpenAI-family compatibility differences are still honored through the scoped
  lookup identity;
- one future parameter-policy update automatically applies everywhere the shared
  helper is used.

Sanitization behavior:

- omit unsupported parameters;
- omit unknown parameters conservatively;
- clamp or validate numeric values when the capability range is explicit;
- honor `default-only` by omission rather than forcing a serialized default;
- keep the translator-specific request contract otherwise unchanged.

This is the behavior that prevents the current class of `400 unsupported_value`
and similar parameter-shape failures.

## Auto-Learning From Provider Errors

The first version should support bounded auto-learning from clearly classifiable
provider rejections.

When a translator receives a provider `400` whose error body clearly indicates a
parameter incompatibility:

1. record a sanitized `LlmModelCapabilityObservation`;
2. promote a conservative `LlmModelCapabilityRule` for the exact model only;
3. use that rule on subsequent requests.

Promotion rules:

- auto-promotion may create or update `ExactModel` rules;
- auto-promotion must not automatically create `FamilyPrefix` rules;
- ambiguous or weakly classified errors create observations only and no rule;
- success does not auto-promote support because one accepted call does not prove
  global compatibility.

This keeps learning useful without allowing one noisy endpoint to contaminate a
whole family.

## Logging And Diagnostics

Capability-related diagnostics should remain concise and sanitized.

Allowed diagnostics:

- effective provider/model scope
- parameter name
- support decision
- rule source
- status code
- short sanitized provider error excerpt

Disallowed diagnostics:

- raw request payloads
- prompts
- glossary contents
- API keys or auth headers
- full provider response bodies when they may echo sensitive input

## Testing Strategy

The first implementation should add focused automated coverage for:

1. `Resolver precedence`
   - exact-model overrides beat family rules;
   - family rules beat static defaults;
   - ambiguous conflicts resolve conservatively.

2. `Payload sanitization`
   - unsupported and unknown parameters are omitted;
   - default-only parameters are omitted;
   - explicit supported ranges clamp or validate correctly.

3. `Error classification and learning`
   - classifiable `400` errors create observations and exact-model rules;
   - ambiguous errors do not promote rules;
   - no auto-promotion to family rules occurs.

4. `UI gating helpers`
   - controls remain visible;
   - controls disable correctly;
   - tooltip text reflects rule source and reason;
   - range-aware controls honor policy ranges.

5. `Persistence`
   - additive EF migration succeeds;
   - startup can load an empty or partially populated capability DB safely.

`Echoglossian.Mock.Tests` may be used only to verify startup and migration
integration if needed. Capability correctness itself should remain covered by
unit tests rather than in-game-only validation.

## Acceptance

This design is complete when the implementation can demonstrate that:

- selecting an incompatible LLM model disables unsupported configuration
  controls with explanatory tooltips;
- the runtime omits unsupported or unknown parameters before sending provider
  requests;
- clearly classifiable provider `400` rejections can teach an exact-model
  conservative rule without operator intervention;
- live-model refresh remains the only refresh entrypoint and does not block
  Dalamud or ImGui callbacks;
- the same effective policy governs both UI and runtime behavior across the LLM
  engines in scope.
